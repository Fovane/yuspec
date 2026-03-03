#include "yuspec_rt/runtime.h"
#include <sstream>

namespace yuspec_rt {

static void execute_stmt(World& w, const yuspec::Stmt& s) {
  using yuspec::StmtKind;
  switch (s.kind) {
    case StmtKind::CreateTerrain:
      w.create_terrain(s.w, s.h);
      break;

    case StmtKind::CreatePlayer:
      w.create_player(s.playerHealth, s.playerMana);
      break;

    case StmtKind::CreateHostiles:
      w.create_hostiles(s.hostileCount, s.hostileHealth);
      break;

    case StmtKind::AttachLogic:
      if (s.logic.name == "move" && s.target.value == "player") {
        w.attach_move_to_player();
      } else if (s.logic.name == "chase" && s.target.value == "hostile") {
        // apply to existing hostiles (if any)
        for (auto& e : w.entities_mut()) {
          if (e.tag == "hostile") e.hasChase = true;
        }
      } else if (s.logic.name == "contact_damage" && s.target.value == "hostile") {
        int dmg = 0;
        auto it = s.logic.params.find("damage");
        if (it != s.logic.params.end()) dmg = it->second;
        for (auto& e : w.entities_mut()) {
          if (e.tag == "hostile") {
            e.hasContactDamage = true;
            e.contactDamage = dmg;
          }
        }
      }
      break;

    case StmtKind::Spawn:
      // For v0.1 we only support hostile random spawn
      w.spawn_hostiles_random();
      break;
  }
}

RunResult run_program_v01(const yuspec::Program& program, const RunConfig& cfg) {
  World w(cfg.seed);

  // 1) Execute DSL statements in order
  for (const auto& st : program.statements) {
    execute_stmt(w, st);
  }

  // 2) After spawn, if script attached hostile logic before spawn,
  // our World::spawn_hostiles_random tries to inherit from existing hostiles.
  // But in common scripts, hostiles are created (count) then logic attached then spawn.
  // There were no existing hostiles at attach time. So we do an extra pass:
  bool wantsChase = false;
  bool wantsCD = false;
  int cd = 0;
  for (const auto& st : program.statements) {
    if (st.kind == yuspec::StmtKind::AttachLogic && st.target.value == "hostile") {
      if (st.logic.name == "chase") wantsChase = true;
      if (st.logic.name == "contact_damage") {
        wantsCD = true;
        auto it = st.logic.params.find("damage");
        if (it != st.logic.params.end()) cd = it->second;
      }
    }
  }
  if (wantsChase || wantsCD) {
    for (auto& e : w.entities_mut()) {
      if (e.tag != "hostile") continue;
      if (wantsChase) e.hasChase = true;
      if (wantsCD) { e.hasContactDamage = true; e.contactDamage = cd; }
    }
  }

  std::ostringstream out;

  // 3) Simulate
  for (int t = 0; t < cfg.ticks; t++) {
    w.tick(cfg.dt);

    if (cfg.verbose) {
      auto* p = w.player();
      if (p) {
        out << "tick " << t << " player=(" << p->pos.x << "," << p->pos.y
            << ") hp=" << p->stats.health << "\n";
      }
    }

    // stop if player died
    auto* p = w.player();
    if (p && p->stats.health <= 0) {
      out << "END: player died at tick " << t << "\n";
      break;
    }
  }

  // final summary
  auto* p = w.player();
  if (p) {
    out << "FINAL: player hp=" << p->stats.health
        << " pos=(" << p->pos.x << "," << p->pos.y << ")\n";
  }
  int hostileCount = 0;
  for (const auto& e : w.entities()) if (e.tag == "hostile") hostileCount++;
  out << "FINAL: hostiles=" << hostileCount << "\n";

  return RunResult{true, out.str()};
}

} // namespace yuspec_rt