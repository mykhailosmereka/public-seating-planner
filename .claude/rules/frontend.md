---
paths:
  - "src/frontend/**"
---
# Frontend conventions
- Feature folders under `src/frontend/src/features/{feature}/`: `api.ts` (typed `fetch` wrappers taking an `AbortSignal`, throwing on `!response.ok`), `{Component}.tsx`, co-located `{Component}.css`. `App.tsx` only composes features.
- Call the backend with relative `/api/...` URLs (Vite proxies to the Server). Handle loading/error/empty states and abort in-flight fetches in `useEffect` cleanup.
- Reuse the global classes/tokens in `App.css`/`index.css` (`card`, `section-header`, `refresh-button`, `error-message`, `loading-skeleton`, `visually-hidden`, `--item-bg` etc.) before adding new ones.
