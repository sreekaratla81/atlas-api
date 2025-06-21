# Governance - Atlas Homestays Repository

## 🔒 Branching Policy

- `main` is production-ready and protected.
- `dev` is the working branch for all merges from feature branches.
- `feature/*` branches should be used for individual tasks and features.

## ✅ PR Policy

- All changes must go through Pull Requests.
- `main` must only be updated via a PR from `dev`.
- Minimum 1 review is required before merging to `main`.

## 🚫 No Secrets Policy

- `.env` files must not be committed.
- Use `.env.example` instead and store real secrets in GitHub Actions Secrets or environment managers.
