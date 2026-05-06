# InPost Smart Finder (ASP.NET Core MVC)

## What I built

This project is a focused "smart finder" for InPost points in Europe.  
It fetches real data from the official InPost API and ranks points by user intent:

- country and city filters,
- optional function requirements,
- optional "24/7 only" and payment requirements,
- optional proximity ranking using a reference location,
- optional preferred point type (`parcel_locker`, `pop`, `pok`) and location type (`Outdoor`, `Indoor`).

API used:
`GET https://api-global-points.easypack24.net/v1/points`

## Why this scope

The assignment is intentionally open-ended, so I chose a compact but product-oriented slice:

- robust API integration (pagination, caching, defensive mapping),
- explainable ranking logic (score breakdown visible in UI),
- practical UX improvements (browser geolocation, pagination, readable result table),
- unit tests for filtering/scoring behavior.

This keeps complexity controlled while showing architecture decisions and iterative improvement.

## Tech stack

- .NET 10
- ASP.NET Core MVC
- xUnit

## Project structure

- `src/InpostTask.Web` - MVC app
  - `Services/InpostApiClient.cs` - API communication + mapping + pagination + cache
  - `Services/PointSearchService.cs` - filtering and scoring
  - `Controllers/PointsController.cs` - search flow + results paging
  - `Views/Points/*` - search form and results table
- `tests/InpostTask.Tests` - unit tests for search/scoring rules

## How to run

### 1) Restore and build

```bash
dotnet restore inpost_task.sln
dotnet build inpost_task.sln
```

### 2) Run app

```bash
dotnet run --project src/InpostTask.Web/InpostTask.Web.csproj
```

Open the URL shown in terminal (usually `http://localhost:5209`).

### 3) Run tests

```bash
dotnet test inpost_task.sln
```

## Deploy (Render, auto deploy from GitHub)

This repository includes:

- `Dockerfile` for containerized ASP.NET Core deployment,
- `render.yaml` blueprint for Render web service configuration.

### One-time setup on Render

1. Open [Render Dashboard](https://dashboard.render.com/) and choose **New +** -> **Blueprint**.
2. Connect your GitHub account/repository.
3. Select this repository and confirm blueprint creation.
4. Render will detect `render.yaml`, create the web service, and deploy from branch `main`.

After that, every push to `main` triggers auto deploy.

## Live demo

The app is deployed and publicly available at:

- [https://inpost-smart-finder.onrender.com](https://inpost-smart-finder.onrender.com)

## Ranking logic (current)

The score is designed to be transparent and practical:

- `+60` for `status = Operating` (`-40` otherwise),
- `+0..100` based on distance (when reference coordinates are provided),
- `+20` when point type matches preferred type,
- `+8` when location type matches preferred location type,
- `+5` for each matched required function,
- `+10` when 24/7 is required and satisfied (`+2` bonus if 24/7 without requirement),
- `+5` when payment is required and satisfied.

Results are sorted by:
1. score descending,
2. distance ascending (if available),
3. city/name.

## Key API observations and trade-offs

- Data quality differs by field and country; parsing is defensive and null-safe.
- `functions` are often repetitive in local clusters, so they are useful but not dominant in scoring.
- `distance` is meaningful only when API receives `relative_point=lat,lng`.
- Some fields (like locker availability details) are often `NO_DATA`, so they were not used as hard signals.
- Caching is memory-based (10 min) to reduce repeated external calls and keep UX responsive.

## UX choices

- "Use my current location" button uses browser geolocation.
- Manual coordinates remain available under an advanced section.
- Long feature lists are collapsed in results, with expandable details.
- Server-side pagination allows browsing beyond first 20 items.

## What I would improve next

- Add address-to-coordinates geocoding (so users do not need coordinates at all).
- Add map view (Leaflet/OpenStreetMap) with score and distance overlays.
- Add score weight configuration in UI for user personalization.
- Add integration tests with mocked HTTP responses.
