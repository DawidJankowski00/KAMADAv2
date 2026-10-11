# Admin parity: web panel vs. phone app

The phone app's admin role shows the **same pages** as the web admin panel: both apps route the components of
`src/Web/Kamada.Web.Admin`, and both menus are rendered from `AdminNavigation`. Parity of features therefore comes from
sharing code; this checklist records that every page and action is also **usable on a phone** (375 × 812) and that the
web panel is unchanged at desktop width.

Checked on 2026-10-11 in the browser preview: phone app at 375 × 812 (touch emulation, account `kierownik` in the
Administrator role) and the web panel at 1366 × 850. "Full screen" = the dialog fills the phone screen
(`kamada-dialog-phone-fullscreen`); confirmations stay small dialogs. No page or dialog scrolls sideways on the phone.
Planning a delivery run was exercised end to end on the phone (create, add orders, optimize and apply, reorder, cancel); the
other actions were checked for layout (the control is reachable, its dialog opens full screen) because their logic is
the same code as in the web panel.

| Area | Page / action | Phone |
|---|---|---|
| Navigation | Menu with all sections (drawer, closes after navigating) | ✓ |
| Navigation | Role switch Kurier ⇄ Administrator, "Przełącz na …" on a page of the other role | ✓ |
| Dashboard | Order counts per status (two per row), "Pokaż" filters the order list | ✓ |
| Dashboard | Today's runs, links to all runs and to a new run | ✓ |
| Orders | List: search, status and date filters, paging (cards) | ✓ |
| Orders | Details: items (SKU and unit price under the name), payments, delivery map | ✓ |
| Orders | Register payment (full screen), mark ready for delivery, cancel | ✓ |
| Orders | Confirm / reject transfer, refund (actions in their own row) | ✓ |
| Orders | New order: assistant panel, client search / new client, address, product lines | ✓ |
| Clients | List: search, status filter (cards) | ✓ |
| Clients | Details: edit, activate / deactivate, addresses, client's orders, map | ✓ |
| Clients | Add / edit address, pin location on the map, re-geocode, archive (full screen) | ✓ |
| Delivery runs | List: search, status, courier and date filters (cards) | ✓ |
| Delivery runs | New run / edit run form | ✓ |
| Delivery runs | Add orders (full screen, selection count above the buttons) | ✓ |
| Delivery runs | Optimize: preview and apply (full screen, map below the list) | ✓ |
| Delivery runs | Reorder stops with the arrows, save / restore order (drag and drop hidden on touch) | ✓ |
| Delivery runs | Approve, cancel, remove a stop, navigation links | ✓ |
| Delivery runs | Run in progress / completed: stop status and arrival under each stop | ✓ |
| Route locations | List with map, add / edit, pin on the map (full screen), activate / deactivate | ✓ |
| Products | List: search, category and status filters, sorting ("Sortuj według" select) | ✓ |
| Products | Add / edit product, change stock (full screen), activate / deactivate | ✓ |
| Categories | Tree, add root / subcategory, rename / move (full screen), delete | ✓ |
| Workers | List: search, role and status filters | ✓ |
| Workers | Add / edit worker with roles, reset password (full screen), activate / deactivate | ✓ |

Desktop width (web panel): tables show every column, dialogs keep their sizes, maps keep their heights, drag and drop
of run stops works as before.
