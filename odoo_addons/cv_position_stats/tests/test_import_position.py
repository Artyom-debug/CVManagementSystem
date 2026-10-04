import os
from types import SimpleNamespace
from unittest.mock import patch

from odoo.exceptions import UserError
from odoo.tests.common import TransactionCase


class TestPositionImport(TransactionCase):
    def setUp(self):
        super().setUp()
        self.admin = self.env.ref("base.user_admin")
        self.url = "https://cv-management-system-zk7q.onrender.com"
        self.position_id = "11111111-1111-4111-8111-111111111111"
        self.numeric_id = "22222222-2222-4222-8222-222222222222"
        self.dropdown_id = "33333333-3333-4333-8333-333333333333"
        environment = patch.dict(os.environ, {"CV_MANAGEMENT_API_URL": self.url})
        environment.start()
        self.addCleanup(environment.stop)

    def _wizard(self, token):
        return self.env["cv.position.import.wizard"].with_user(self.admin).create({
            "api_token": token,
        })

    def _payload(self):
        return {
            "positionId": self.position_id,
            "name": ".NET Developer",
            "description": "Build services",
            "createdAt": "2026-10-04T10:00:00Z",
            "publishedCvCount": 2,
            "attributes": [
                {
                    "attributeId": self.numeric_id,
                    "name": "Years of experience",
                    "type": "Numeric",
                    "displayOrder": 0,
                    "filledCount": 2,
                    "numericMinimum": 2.0,
                    "numericMaximum": 4.0,
                    "numericAverage": 3.0,
                    "topValues": [],
                },
                {
                    "attributeId": self.dropdown_id,
                    "name": "Work mode",
                    "type": "Dropdown",
                    "displayOrder": 1,
                    "filledCount": 2,
                    "topValues": [{"value": "Remote", "count": 2}],
                },
            ],
        }

    def test_import_and_refresh_existing_position(self):
        first = self._wizard("odoo_" + "A" * 64)
        with patch("requests.post", return_value=SimpleNamespace(
            status_code=200, json=lambda: self._payload()
        )) as post:
            result = first.action_import()

        post.assert_called_once()
        self.assertEqual(post.call_args.args[0], self.url + "/api/odoo/position-statistics")
        self.assertEqual(post.call_args.kwargs["headers"]["Authorization"], "Bearer odoo_" + "A" * 64)
        self.assertFalse(post.call_args.kwargs["allow_redirects"])
        self.assertFalse(first.exists())

        position = self.env["cv.position.import"].browse(result["res_id"])
        self.assertEqual(position.source_position_id, self.position_id)
        self.assertEqual(position.published_cv_count, 2)
        self.assertEqual(len(position.attribute_stat_ids), 2)
        self.assertEqual(position.attribute_stat_ids.filtered(
            lambda stat: stat.source_attribute_id == self.dropdown_id
        ).top_value_ids.name, "Remote")

        updated = self._payload()
        updated["name"] = "Senior .NET Developer"
        updated["publishedCvCount"] = 3
        updated["attributes"] = [updated["attributes"][1]]
        updated["attributes"][0]["topValues"] = [{"value": "Hybrid", "count": 3}]

        with patch("requests.post", return_value=SimpleNamespace(
            status_code=200, json=lambda: updated
        )):
            second = self._wizard("odoo_" + "B" * 64)
            second_result = second.action_import()

        position.invalidate_recordset()
        self.assertEqual(second_result["res_id"], position.id)
        self.assertEqual(position.name, "Senior .NET Developer")
        self.assertEqual(position.published_cv_count, 3)
        self.assertEqual(len(position.attribute_stat_ids), 1)
        self.assertEqual(position.attribute_stat_ids.source_attribute_id, self.dropdown_id)
        self.assertEqual(position.attribute_stat_ids.top_value_ids.name, "Hybrid")

    def test_invalid_token_does_not_create_position(self):
        wizard = self._wizard("odoo_" + "C" * 64)
        with patch("requests.post", return_value=SimpleNamespace(status_code=401)):
            with self.assertRaises(UserError):
                wizard.action_import()
        self.assertFalse(self.env["cv.position.import"].search([
            ("source_position_id", "=", self.position_id)
        ]))

    def test_remote_http_url_is_rejected_before_sending_token(self):
        wizard = self._wizard("odoo_" + "D" * 64)
        with patch.dict(os.environ, {"CV_MANAGEMENT_API_URL": "http://example.com"}):
            with patch("requests.post") as post:
                with self.assertRaises(UserError):
                    wizard.action_import()
        post.assert_not_called()

    def test_missing_api_url_is_rejected_before_sending_token(self):
        wizard = self._wizard("odoo_" + "E" * 64)
        with patch.dict(os.environ, {"CV_MANAGEMENT_API_URL": ""}):
            with patch("requests.post") as post:
                with self.assertRaises(UserError):
                    wizard.action_import()
        post.assert_not_called()

    def test_malformed_token_is_rejected_before_request(self):
        wizard = self._wizard("not-an-odoo-token")
        with patch("requests.post") as post:
            with self.assertRaises(UserError):
                wizard.action_import()
        post.assert_not_called()
