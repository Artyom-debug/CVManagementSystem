import logging
import math
import os
from datetime import date, datetime, timezone
from urllib.parse import urlsplit
from uuid import UUID

import requests

from odoo import _, fields, models
from odoo.exceptions import UserError


_logger = logging.getLogger(__name__)
_ATTRIBUTE_TYPES = {
    "String", "Text", "Image", "Numeric", "Date", "Period", "Checkbox", "Dropdown"
}
_STATISTICS_PATH = "/api/odoo/position-statistics"
_DEFAULT_API_URL = "https://cv-management-system-zk7q.onrender.com"


def _required_string(data, key):
    value = data.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError("Missing or invalid field: %s" % key)
    return value.strip()


def _nonnegative_integer(data, key, default=None):
    value = data.get(key, default)
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ValueError("Missing or invalid field: %s" % key)
    return value


def _optional_datetime(value):
    if value is None:
        return False
    if not isinstance(value, str):
        raise ValueError("Invalid date-time value")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError("Date-time must include a timezone")
    return parsed.astimezone(timezone.utc).replace(tzinfo=None).strftime("%Y-%m-%d %H:%M:%S")


def _optional_date(value):
    if value is None:
        return False
    if not isinstance(value, str):
        raise ValueError("Invalid date value")
    return date.fromisoformat(value).isoformat()


def _optional_number(value):
    if value is None:
        return False
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError("Invalid numeric statistic")
    return value


class ImportPositionWizard(models.TransientModel):
    _name = "cv.position.import.wizard"
    _description = "Import CV Position Statistics"
    _transient_max_hours = 0.1

    api_token = fields.Char(string="Position API Token", required=True, copy=False)

    def action_import(self):
        self.ensure_one()
        if not self.env.user.has_group("base.group_system"):
            raise UserError(_("Only an Odoo administrator can import positions."))
        if not self.api_token or not self.api_token.strip():
            raise UserError(_("Enter a position API token."))

        configured_url = os.environ.get("CV_MANAGEMENT_API_URL", _DEFAULT_API_URL).strip()
        if not configured_url:
            raise UserError(_("Configure CV_MANAGEMENT_API_URL on the Odoo server."))

        try:
            parsed_url = urlsplit(configured_url)
        except ValueError as exc:
            raise UserError(_("The configured CVManagementSystem API URL is invalid.")) from exc
        if parsed_url.scheme not in {"https", "http"} or not parsed_url.netloc:
            raise UserError(_("The configured CVManagementSystem API URL is invalid."))
        if parsed_url.scheme == "http" and parsed_url.hostname not in {
            "localhost", "127.0.0.1", "host.docker.internal"
        }:
            raise UserError(_("Use HTTPS for a remote CVManagementSystem API."))
        if parsed_url.username or parsed_url.password or parsed_url.query or parsed_url.fragment:
            raise UserError(_("The API URL must not contain credentials, query or fragment."))
        if parsed_url.path not in {"", "/", _STATISTICS_PATH}:
            raise UserError(_("Use the site URL or the Odoo statistics API URL."))

        api_url = "%s://%s%s" % (
            parsed_url.scheme,
            parsed_url.netloc,
            _STATISTICS_PATH,
        )

        try:
            response = requests.post(
                api_url,
                headers={"Authorization": "Bearer %s" % self.api_token.strip(),
                         "Accept": "application/json"},
                timeout=(5, 90),  
                allow_redirects=False,
            )
        except requests.RequestException as exc:
            _logger.warning("CVManagementSystem API request failed: %s", type(exc).__name__)
            raise UserError(_("Could not reach CVManagementSystem. Check its URL and availability.")) from exc

        if response.status_code in (401, 403):
            raise UserError(_("The position API token is invalid, expired, or already used."))
        if response.status_code == 404:
            raise UserError(_("The position linked to this token was not found."))
        if response.status_code != 200:
            raise UserError(_("CVManagementSystem returned HTTP %(status)s.") % {
                "status": response.status_code
            })

        try:
            position_values, attributes = self._parse_response(response.json())
        except (ValueError, TypeError, KeyError) as exc:
            _logger.warning("CVManagementSystem returned an invalid import response: %s", exc)
            raise UserError(_(
                "CVManagementSystem returned an invalid statistics response. "
                "The one-time token may have been used; generate a new one before retrying."
            )) from exc

        positions = self.env["cv.position.import"].sudo()
        position = positions.search(
            [("source_position_id", "=", position_values["source_position_id"])], limit=1
        )
        position_values["imported_at"] = fields.Datetime.now()
        if position:
            position.write(position_values)
        else:
            position = positions.create(position_values)

        stats_model = self.env["cv.position.attribute.stat"].sudo()
        top_model = self.env["cv.position.top.value"].sudo()
        imported_attribute_ids = set()
        for statistic_values, top_values in attributes:
            attribute_id = statistic_values["source_attribute_id"]
            imported_attribute_ids.add(attribute_id)
            statistic = stats_model.search([
                ("position_id", "=", position.id),
                ("source_attribute_id", "=", attribute_id),
            ], limit=1)
            if statistic:
                statistic.write(statistic_values)
                statistic.top_value_ids.unlink()
            else:
                statistic = stats_model.create({
                    **statistic_values, "position_id": position.id
                })
            for top_value in top_values:
                top_model.create({**top_value, "attribute_stat_id": statistic.id})

        for statistic in position.attribute_stat_ids:
            if statistic.source_attribute_id not in imported_attribute_ids:
                statistic.unlink()

        self.unlink() 
        return {
            "type": "ir.actions.act_window",
            "name": _("Imported Position"),
            "res_model": "cv.position.import",
            "res_id": position.id,
            "view_mode": "form",
            "target": "current",
        }

    def _parse_response(self, payload):
        if not isinstance(payload, dict):
            raise ValueError("Root must be an object")
        position_id = str(UUID(_required_string(payload, "positionId")))
        position_values = {
            "source_position_id": position_id,
            "name": _required_string(payload, "name"),
            "description": payload.get("description") or False,
            "source_created_at": _optional_datetime(payload.get("createdAt")),
            "published_cv_count": _nonnegative_integer(payload, "publishedCvCount"),
        }
        if position_values["description"] is not False and not isinstance(position_values["description"], str):
            raise ValueError("Invalid description")

        raw_attributes = payload.get("attributes")
        if not isinstance(raw_attributes, list):
            raise ValueError("Missing attributes array")
        attributes = []
        seen_ids = set()
        for item in raw_attributes:
            if not isinstance(item, dict):
                raise ValueError("Invalid attribute")
            attribute_id = str(UUID(_required_string(item, "attributeId")))
            if attribute_id in seen_ids:
                raise ValueError("Duplicate attributeId")
            seen_ids.add(attribute_id)
            attribute_type = _required_string(item, "type")
            if attribute_type not in _ATTRIBUTE_TYPES:
                raise ValueError("Unknown attribute type")
            display_order = item.get("displayOrder", 0)
            if isinstance(display_order, bool) or not isinstance(display_order, int):
                raise ValueError("Invalid displayOrder")
            statistic_values = {
                "source_attribute_id": attribute_id,
                "name": _required_string(item, "name"),
                "attribute_type": attribute_type,
                "display_order": display_order,
                "filled_count": _nonnegative_integer(item, "filledCount"),
                "numeric_minimum": _optional_number(item.get("numericMinimum")),
                "numeric_maximum": _optional_number(item.get("numericMaximum")),
                "numeric_average": _optional_number(item.get("numericAverage")),
                "earliest_date": _optional_date(item.get("earliestDate")),
                "latest_date": _optional_date(item.get("latestDate")),
                "earliest_period_start": _optional_date(item.get("earliestPeriodStart")),
                "latest_period_end": _optional_date(item.get("latestPeriodEnd")),
                "true_count": _nonnegative_integer(item, "trueCount", 0),
                "false_count": _nonnegative_integer(item, "falseCount", 0),
            }
            raw_top_values = item.get("topValues", [])
            if not isinstance(raw_top_values, list):
                raise ValueError("Invalid topValues")
            top_values = []
            for top in raw_top_values:
                if not isinstance(top, dict):
                    raise ValueError("Invalid top value")
                top_values.append({
                    "name": _required_string(top, "value"),
                    "count": _nonnegative_integer(top, "count"),
                })
            attributes.append((statistic_values, top_values))
        return position_values, attributes
