from odoo import fields, models


class ImportedPosition(models.Model):
    _name = "cv.position.import"
    _description = "Imported CV Position"
    _order = "name, id"

    _source_position_unique = models.Constraint(
        "UNIQUE(source_position_id)",
        "This position has already been imported.",
    )

    source_position_id = fields.Char(required=True, index=True, copy=False, size=36)
    name = fields.Char(required=True)
    description = fields.Text()
    published_cv_count = fields.Integer(default=0)
    source_created_at = fields.Datetime()
    imported_at = fields.Datetime(required=True)
    attribute_stat_ids = fields.One2many(
        "cv.position.attribute.stat",
        "position_id",
        string="Attribute Statistics",
    )


class PositionAttributeStatistic(models.Model):
    _name = "cv.position.attribute.stat"
    _description = "Imported Position Attribute Statistic"
    _order = "display_order, id"

    _position_attribute_unique = models.Constraint(
        "UNIQUE(position_id, source_attribute_id)",
        "This attribute has already been imported for the position.",
    )

    position_id = fields.Many2one(
        "cv.position.import",
        required=True,
        ondelete="cascade",
        index=True,
    )
    source_attribute_id = fields.Char(required=True, size=36)
    name = fields.Char(required=True)
    attribute_type = fields.Selection(
        [
            ("String", "String"),
            ("Text", "Text"),
            ("Image", "Image"),
            ("Numeric", "Numeric"),
            ("Date", "Date"),
            ("Period", "Period"),
            ("Checkbox", "Checkbox"),
            ("Dropdown", "Dropdown"),
        ],
        required=True,
    )
    display_order = fields.Integer(default=0)
    filled_count = fields.Integer(default=0)

    numeric_minimum = fields.Float()
    numeric_maximum = fields.Float()
    numeric_average = fields.Float()

    earliest_date = fields.Date()
    latest_date = fields.Date()
    earliest_period_start = fields.Date()
    latest_period_end = fields.Date()

    true_count = fields.Integer(default=0)
    false_count = fields.Integer(default=0)
    top_value_ids = fields.One2many(
        "cv.position.top.value",
        "attribute_stat_id",
        string="Most Popular Values",
    )


class PopularAttributeValue(models.Model):
    _name = "cv.position.top.value"
    _description = "Popular Position Attribute Value"
    _order = "count desc, id"

    attribute_stat_id = fields.Many2one(
        "cv.position.attribute.stat",
        required=True,
        ondelete="cascade",
        index=True,
    )
    name = fields.Char(required=True)
    count = fields.Integer(default=0)
