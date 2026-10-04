FROM odoo:19.0

COPY --chown=odoo:odoo odoo_addons/cv_position_stats /mnt/extra-addons/cv_position_stats
COPY --chown=odoo:odoo --chmod=755 odoo_addons/start-prod.sh /usr/local/bin/start-odoo-prod

CMD ["/usr/local/bin/start-odoo-prod"]
