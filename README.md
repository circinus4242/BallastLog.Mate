# Ballast Log Mate ⚓

**Ballast Log Mate** is a tiny, portable web app for keeping ballast operation records.  
Just put the EXE on a USB stick, run it, and use it in your browser — no install, no setup.

---

## What it does

- **Setup**  
  Enter ship name, class, max flowrate, and add tanks.  
  Import/export ship profile as JSON.

- **Status**  
  See all tanks with their current volumes.  
  Export tank list as CSV.

- **Operations**  
  Log BALLAST, DEBALLAST, TRANSFER, or MISC ops.  
  Choose tanks (and SEA), enter amounts, auto-calc totals & flowrate.  
  See before/after volumes and mark as recorded in Log Book or FM-123.

---

## How to use

1. Copy the published folder to a USB stick.
2. Run `BallastLog.Mate.exe`.
3. The app opens at [http://127.0.0.1:7777](http://127.0.0.1:7777).
4. All data is stored locally in `data/ballast.db` (next to the EXE).

---

Enjoy easier ballast logging 🚢
