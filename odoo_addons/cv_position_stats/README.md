# CV Position Statistics (Odoo 19)

This module imports a position and aggregated CV statistics from CVManagementSystem. It is a read-only viewer: imports are initiated in Odoo, while position data remains owned by CVManagementSystem. Importing the same position again updates its Odoo record and replaces stale statistics.

## Local setup

1. Start the Odoo stack from this directory: `docker compose up -d`.
2. Open `http://localhost:8069`, create an Odoo database if necessary, enable developer mode, and update the Apps list.
3. Remove the Apps-only search filter, search for **CV Position Statistics**, and install it. After code updates, upgrade the module in Apps.
4. In CVManagementSystem, generate a token using `POST /api/positions/{positionId}/odoo-token` while signed in as a recruiter or administrator. Copy the token from the response.
5. In Odoo, open **CV Position Statistics → Import by Token** as an Odoo administrator. Enter the token and click **Import**.

Odoo knows the C# API address from the `CV_MANAGEMENT_API_URL` server environment variable. The module and Compose file default to `https://cv-management-system-zk7q.onrender.com`, so the user never enters a URL in the import form. To call a C# API running on the Windows host while Odoo runs in Docker, set `CV_MANAGEMENT_API_URL=http://host.docker.internal:8080` for the Odoo container (adjust the port), then recreate that container. A full URL ending in `/api/odoo/position-statistics` also works. Tokens are not retained after a successful import.

The token is valid for 24 hours and can be used once. If Odoo fails after C# has returned the statistics, generate a new token before retrying. The Odoo administrator can repeat an import with a new token to refresh an existing position. API traffic to a remote host must use HTTPS.
