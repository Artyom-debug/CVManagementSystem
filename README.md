<div align="center">

### Shape a profile. Find a position. Publish a CV.

A role-aware workspace for candidates, recruiters, and administrators.

<p>
  <img src="https://img.shields.io/badge/.NET-9-3f4550?style=flat&logo=dotnet&logoColor=c9d1d9" alt=".NET 9">
  <img src="https://img.shields.io/badge/React-19-3f4550?style=flat&logo=react&logoColor=c9d1d9" alt="React 19">
  <img src="https://img.shields.io/badge/PostgreSQL-EF_Core-3f4550?style=flat&logo=postgresql&logoColor=c9d1d9" alt="PostgreSQL and Entity Framework Core">
  <img src="https://img.shields.io/badge/Redis-cache-3f4550?style=flat&logo=redis&logoColor=c9d1d9" alt="Redis cache">
  <img src="https://img.shields.io/badge/Docker-ready-3f4550?style=flat&logo=docker&logoColor=c9d1d9" alt="Docker ready">
</p>

[Live demo](https://cv-management-system-zk7q.onrender.com/) · [Run locally](#run-it) 

</div>

CV management system built around a shared library of typed attributes.
Candidates maintain a profile and projects; recruiters assemble positions from
attributes, tags, and access rules. A CV connects one profile to one position and
can be published when every attribute in its template has a value.

## The working parts

- **One attribute library.** String, text, number, date, period, checkbox,
  dropdown, and image attributes can be reused across profiles and positions.
  System attributes are included in every CV.
- **Positions with access rules.** Recruiters compose position templates and
  rules; the available-positions query evaluates them against the candidate's
  profile in PostgreSQL before paging the result.
- **CVs backed by profiles.** CV attribute values come from the profile. A
  candidate can remove a CV from their profile without deleting its position
  record; administrators have a separate permanent-delete operation.
- **Role-aware workflows.** Candidates manage CVs, recruiters manage positions
  and review submissions, and administrators manage users and system data.
  Authorization is enforced by API policies and command checks.
- **Cache invalidation by dependency.** Read queries cache pages and details in
  Redis. Domain events invalidate entries associated with changed attributes,
  profiles, positions, or CVs.
- **Direct image uploads.** The API signs Cloudinary upload parameters; the
  browser uploads the file, while the application stores its public ID.
- **A responsive client.** React provides RU/EN interface text, light and dark
  themes, paged infinite lists, profile editing, and position builders.

## Architecture

```text
Client (React + TypeScript + Vite)
   │  same-origin /api requests
   ▼
Web (ASP.NET Core controllers, authentication, authorization)
   │
   ▼
Application (commands, queries, validation, event handlers)
   │                         │
   ▼                         ▼
Domain (entities, rules)   Infrastructure (EF Core, Identity,
                              Redis, Cloudinary, Gmail)
                              │
                              ├── PostgreSQL — durable data
                              └── Redis — cache and pending registrations
```

The production Dockerfile builds React and publishes its files into ASP.NET's
`wwwroot`. The API and frontend therefore share a public origin: `/` serves
the application and `/api/*` serves the controllers. Docker Compose keeps a
separate nginx client container for local testing.

## Run it

Requirements: Docker with Compose, a PostgreSQL database, a Redis instance,
and Cloudinary credentials. Compose starts the web and client containers; it
does not provision PostgreSQL or Redis.

1. Create `.env` in the repository root. Use your own values; do not commit it:

   ```dotenv
   ConnectionStrings__Postgres=Host=YOUR_HOST;Port=5432;Database=YOUR_DB;Username=YOUR_USER;Password=YOUR_PASSWORD
   ConnectionStrings__Redis=rediss://default:YOUR_PASSWORD@YOUR_HOST:6379
   Jwt__SecretKey=YOUR_BASE64_ENCODED_32_BYTE_OR_LONGER_SECRET
   AdminSeed__Email=admin@example.com
   AdminSeed__Password=YOUR_ADMIN_PASSWORD
   Cloudinary__CloudName=YOUR_CLOUD_NAME
   Cloudinary__ApiKey=YOUR_API_KEY
   Cloudinary__ApiSecret=YOUR_API_SECRET
   Cloudinary__DeliveryType=upload
   GoogleGmail__ClientId=YOUR_GMAIL_OAUTH_CLIENT_ID
   GoogleGmail__ClientSecret=YOUR_GMAIL_OAUTH_CLIENT_SECRET
   GoogleGmail__RefreshToken=YOUR_GMAIL_REFRESH_TOKEN
   GoogleGmail__SenderEmail=YOUR_SENDER_EMAIL
   GoogleGmail__ConfirmationUrl=http://localhost:3000/api/auth/confirm-email
   Dropbox__AppKey=YOUR_DROPBOX_APP_KEY
   Dropbox__AppSecret=YOUR_DROPBOX_APP_SECRET
   Dropbox__RefreshToken=YOUR_DROPBOX_REFRESH_TOKEN
   Dropbox__Folder=/SupportTickets
   ```

   `Cloudinary__UploadPreset` is optional for the signed upload flow. Add
   `Authentication__Google__ClientId` and `Authentication__Google__ClientSecret`
   to enable Google login; the equivalent Facebook keys are
   `Authentication__Facebook__AppId` and `Authentication__Facebook__AppSecret`.

2. Build and start:

   ```bash
   docker compose up --build -d
   ```

3. Open [the client](http://localhost:3000), [Swagger](http://localhost:8080/swagger),
   or the [health endpoint](http://localhost:8080/health).

At startup the server applies EF Core migrations and seeds the predefined
roles, administrator, and system attributes. PostgreSQL must permit the
`pg_trgm` extension used by the database model.

## Support tickets through Dropbox and Power Automate

Create a scoped Dropbox app with **Full Dropbox** file access and the
`files.content.write` permission. Authorize it for the same Dropbox account
used by the Power Automate Dropbox connection, requesting offline access so
the app receives a refresh token. Put the app key, app secret, and refresh
token in the server settings above; never put them in the browser or commit
them. Create `/SupportTickets` in that Dropbox account and select exactly
that folder in the Power Automate "When a file is created (properties only)"
trigger. An App Folder-scoped Dropbox app instead interprets paths relative
to its app folder, so its trigger folder must be adjusted accordingly.

Authenticated clients submit `POST /api/support-tickets` with a body such as:

```json
{
  "summary": "Cannot submit a CV",
  "priority": "High",
  "pageUrl": "https://example.com/positions/123",
  "positionId": null
}
```

`priority` can be `High`, `Average`, or `Low`. Set `positionId` to an existing
position ID only when the ticket concerns that position. The API supplies
the reporter's email and role, position title, and administrator email
addresses from server-side data. It uploads a unique JSON file with these
camel-case fields: `summary`, `reportedBy`, `position`, `link`, `priority`, and
`adminEmails` (an array of strings). Match the Power Automate Parse JSON
schema to this file. Use a Join action with `;` as the delimiter before
placing `adminEmails` in the mail action's To field. The endpoint reports a
delivery failure if Dropbox rejects the file; it does not claim that the
Power Automate email was delivered.

For a local contract check, run
`dotnet run --project Tests/SupportTicketRegression/SupportTicketRegression.csproj`.

<details>
<summary><strong>Develop without Docker</strong></summary>

Install the .NET 9 SDK and Node.js 22.12+. Configure the same application
settings with ASP.NET user secrets or environment variables, then run:

```bash
dotnet run --project Web/Web.csproj --launch-profile https
```

In a second terminal:

```bash
cd Client
npm ci
npm run dev
```

The Vite client opens at `http://127.0.0.1:5173` and proxies `/api` to
`https://localhost:7240` by default. Set `API_TARGET` in `Client/.env.local`
if your API runs elsewhere.

</details>

<details>
<summary><strong>Render deployment</strong></summary>

Connect the repository as a Docker Web Service using the root `Dockerfile`,
or use `render.yaml` as a Blueprint. Configure the variables above in Render's
Environment page. `ConnectionStrings__Postgres` accepts Render's PostgreSQL
URL; `ConnectionStrings__Redis` accepts a Redis URI. Set
`GoogleGmail__ConfirmationUrl` to
`https://YOUR-SERVICE.onrender.com/api/auth/confirm-email`.

The service exposes `/health` for Render health checks. Google and Facebook
OAuth providers need redirect URIs ending in `/api/auth/signin-google` and
`/api/auth/signin-facebook`, respectively.

</details>

## API at a glance

| Area | Examples |
| --- | --- |
| Authentication | Register, confirm email, resend confirmation, login, refresh, Google/Facebook login |
| Attributes and tags | Browse, search, create, update, delete |
| Profiles | Edit typed values and projects; view a permitted profile |
| Positions | Public and available lists, details, discussion, CV submissions |
| CVs | Create, edit, publish, remove from profile, like |
| Administration | Find users, block or unblock, assign predefined roles |

Swagger is available in the `Development` environment at `/swagger`.

## Checks

```bash
dotnet build CVManagementSystem.slnx
cd Client
npm ci
npm run build
npm test
```

The frontend has a separate [development guide](Client/README.md). The
repository does not declare a license.
