#!/usr/bin/env python3
"""Issue #101 — Messung: warum schiesst niemand zurueck, wenn der Harvester brennt.

Liest die Artefakte eines Laborlaufs und beantwortet die eine Frage, die das
Issue vor dem Bau gestellt hat: war die naechste eigene Kampfeinheit
AUSSER REICHWEITE (Fall A) oder hatte sie einen STEHENDEN ANGRIFFSBEFEHL
(Fall B)? Beides schaltet die Auto-Zielerfassung aus CombatSystem ab.

Reines Lesewerkzeug. Es rechnet nichts nach, was die Simulation schon
aufgeschrieben hat: Reichweiten kommen aus SimDefinitions, der Angriffsbefehl
aus AttackTargetRaw in goals.ndjson.
"""
import json, sys, os

ONE = 65536  # SimFixed Q16

GOAL = {0: "None", 1: "Retreat", 2: "Attack", 3: "Hold", 4: "Advance",
        5: "DefendHome", 6: "Reinforce"}

# attackRangeTiles aus Simulation/Definitions/SimDefinitions.cs
RANGE = {
    "alliance": {12: 7, 13: 10, 14: 8, 15: 9, 16: 10, 17: 20, 11: 10},
    "legion":   {12: 6, 13: 9,  14: 7, 15: 8, 16: 8,  17: 18, 11: 10},
}
COMBAT_ROLES = {12, 13, 14, 15, 16, 17}
ROLE = {2: "harvester", 11: "turm", 12: "infanterie", 13: "panzerabwehr",
        14: "spaeher", 15: "leichterPanzer", 16: "kampfpanzer", 17: "artillerie"}


def load_tracks(path):
    """tracks.ndjson -> {tick: {id: (x, y)}} als laufender Zustand je Tick."""
    frames, pos = {}, {}
    with open(path) as fh:
        for line in fh:
            row = json.loads(line)
            for eid, x, y in row.get("a", []):
                pos[eid] = (x, y)
            for eid, dx, dy in row.get("d", []):
                if eid in pos:
                    pos[eid] = (pos[eid][0] + dx, pos[eid][1] + dy)
            for eid in row.get("x", []):
                pos.pop(eid, None)
            frames[row["t"]] = dict(pos)
    return frames


def at(frames, ticks, t):
    """Position aller Einheiten zum letzten aufgezeichneten Tick <= t."""
    lo, hi, best = 0, len(ticks) - 1, None
    while lo <= hi:
        mid = (lo + hi) // 2
        if ticks[mid] <= t:
            best = ticks[mid]; lo = mid + 1
        else:
            hi = mid - 1
    return frames.get(best, {}), best


def tiles(a, b):
    dx = (a[0] - b[0]) / ONE
    dy = (a[1] - b[1]) / ONE
    return (dx * dx + dy * dy) ** 0.5


def main(run):
    result = json.load(open(os.path.join(run, "result.json")))
    faction = {s["slot"]: s["faction"] for s in result["slots"]}

    meta, damage, deaths = {}, [], []
    with open(os.path.join(run, "events.ndjson")) as fh:
        for line in fh:
            e = json.loads(line)
            if e["k"] == "spawn":
                meta[e["id"]] = (e["slot"], e["role"])
            elif e["k"] == "damage" and e["role"] == 2:
                damage.append(e)
            elif e["k"] == "death":
                deaths.append(e)

    frames = load_tracks(os.path.join(run, "tracks.ndjson"))
    ticks = sorted(frames)

    goals = {}   # (slot, tick) -> [unitrow]
    gticks = {}  # slot -> sortierte Entscheidungsticks
    with open(os.path.join(run, "goals.ndjson")) as fh:
        for line in fh:
            g = json.loads(line)
            goals[(g["s"], g["t"])] = g["u"]
            gticks.setdefault(g["s"], []).append(g["t"])
    for s in gticks:
        gticks[s].sort()

    def goal_frame(slot, t):
        best = None
        for gt in gticks.get(slot, []):
            if gt <= t:
                best = gt
            else:
                break
        return goals.get((slot, best), []), best

    # Beschuss zu Episoden zusammenfassen: Luecke > 60 Ticks = neue Episode.
    episodes, current = [], []
    for e in sorted(damage, key=lambda r: r["t"]):
        if current and e["t"] - current[-1]["t"] > 60:
            episodes.append(current); current = []
        current.append(e)
    if current:
        episodes.append(current)

    print(f"Lauf:      {run}")
    print(f"Verhalten: {result['aiBehaviorId']}   Ausgang: {result['outcome']} "
          f"(Slot {result['winnerSlot']}, Tick {result['decidedTick']})")
    for s in result["slots"]:
        print(f"  Slot {s['slot']}: {s['faction']:8} Profil {s['profile']}")
    print(f"\nHarvester unter Beschuss: {len(damage)} Ereignisse in {len(episodes)} Episoden")
    hdeaths = [d for d in deaths if d["role"] == 2]
    print(f"Tote Harvester: {len(hdeaths)}"
          + (f" (Tick {', '.join(str(d['t']) for d in hdeaths)})" if hdeaths else ""))

    stats = {"A_ausser_reichweite": 0, "B_stehender_befehl": 0,
             "in_reichweite_frei": 0, "keine_einheit": 0}

    for n, ep in enumerate(episodes, 1):
        first, last = ep[0], ep[-1]
        slot = first["slot"]
        fac = faction[slot]
        attackers = [a for a in first.get("by", [])]
        sure = first.get("bySure", 0)
        snap, snap_t = at(frames, ticks, first["t"])
        victim = snap.get(first["id"])
        apos = [snap[a] for a in attackers if a in snap]

        print(f"\n--- Episode {n}: Tick {first['t']}–{last['t']}, "
              f"{len(ep)} Treffer, Opfer {first['id']} (Slot {slot}, {fac})")
        total = sum(d["from"] - d["to"] for d in ep)
        print(f"    Schaden gesamt {total} HP, Angreifer {attackers} "
              f"({'sicher' if sure else 'hergeleitet'})")
        if victim is None or not apos:
            print("    keine Positionsdaten — uebersprungen")
            continue

        rows, gt = goal_frame(slot, first["t"])
        judged = {r[0]: r for r in rows}

        near = []
        for eid, (s, role) in meta.items():
            if s != slot or role not in COMBAT_ROLES or eid not in snap:
                continue
            d = min(tiles(snap[eid], p) for p in apos)
            near.append((d, eid, role))
        near.sort()

        if not near:
            print("    keine eigene Kampfeinheit am Leben")
            stats["keine_einheit"] += 1
            continue

        print(f"    naechste eigene Kampfeinheiten (Entscheidung Tick {gt}, "
              f"Positionen Tick {snap_t}):")
        verdict_taken = False
        for d, eid, role in near[:5]:
            rng = RANGE[fac].get(role, 0)
            row = judged.get(eid)
            goal = GOAL.get(row[1], "?") if row else "nicht beurteilt"
            target = row[3] if row else 0
            inrange = d <= rng
            marks = []
            if not inrange:
                marks.append(f"AUSSER REICHWEITE ({d:.1f} > {rng})")
            if target:
                marks.append(f"stehender Angriffsbefehl auf {target}")
            if inrange and not target:
                marks.append("in Reichweite UND ohne Befehl -> haette feuern muessen")
            print(f"      #{eid} {ROLE.get(role, role):15} {d:6.1f} Felder  "
                  f"Reichweite {rng:2}  Ziel {target:>6}  {goal:11} "
                  f"{' · '.join(marks)}")
            if not verdict_taken:
                verdict_taken = True
                if inrange and not target:
                    stats["in_reichweite_frei"] += 1
                elif target:
                    stats["B_stehender_befehl"] += 1
                else:
                    stats["A_ausser_reichweite"] += 1

    print("\n=== Befund ueber alle Episoden (naechste eigene Kampfeinheit) ===")
    for k, v in stats.items():
        print(f"  {k:24} {v}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "out/i101-feldueberfall")
