set -eu

: "${HOST:?Set HOST to the Odoo PostgreSQL hostname}"
: "${USER:?Set USER to the Odoo PostgreSQL user}"
: "${PASSWORD:?Set PASSWORD to the Odoo PostgreSQL password}"
: "${ODOO_DB_NAME:?Set ODOO_DB_NAME to the existing Odoo database name}"
: "${CV_MANAGEMENT_API_URL:?Set CV_MANAGEMENT_API_URL to the public CVManagementSystem URL}"

case "$ODOO_DB_NAME" in
    *[!a-zA-Z0-9_]*|"")
        echo "ODOO_DB_NAME may contain only letters, digits and underscores." >&2
        exit 1
        ;;
esac

http_port="${PORT:-10000}"
case "$http_port" in
    *[!0-9]*|"")
        echo "PORT must be a numeric HTTP port." >&2
        exit 1
        ;;
esac

export PORT="${ODOO_DB_PORT:-5432}"

exec /entrypoint.sh \
    --http-port="$http_port" \
    --proxy-mode \
    --database="$ODOO_DB_NAME" \
    --db-filter="^${ODOO_DB_NAME}$" \
    --no-database-list

if [ "${ODOO_INIT_DB:-false}" = "true" ]; then
    /entrypoint.sh \
        --database="$ODOO_DB_NAME" \
        --init=base,cv_position_stats \
        --stop-after-init
fi