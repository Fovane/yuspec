#include "yuspec/validate.h"
#include <sstream>

namespace yuspec {

ValidationResult validate_v01(const Program& program) {
  bool hasTerrain = false;
  int terrainCount = 0;

  bool hasPlayer = false;
  bool hasHostiles = false;

  int terrainW = 0, terrainH = 0;
  int hostileCount = 0;

  std::ostringstream out;
  bool ok = true;

  auto fail = [&](const std::string& msg) {
    ok = false;
    out << "ERROR: " << msg << "\n";
  };

  for (const auto& s : program.statements) {
    switch (s.kind) {
      case StmtKind::CreateTerrain:
        terrainCount++;
        hasTerrain = true;
        terrainW = s.w;
        terrainH = s.h;
        if (s.w <= 0 || s.h <= 0) fail("terrain size must be > 0");
        break;

      case StmtKind::CreatePlayer:
        hasPlayer = true;
        if (s.playerHealth <= 0) fail("player health must be > 0");
        if (s.playerMana < 0) fail("player mana must be >= 0");
        break;

      case StmtKind::CreateHostiles:
        hasHostiles = true;
        hostileCount = s.hostileCount;
        if (s.hostileCount < 1) fail("hostile count must be >= 1");
        if (s.hostileHealth <= 0) fail("hostile health must be > 0");
        break;

      case StmtKind::AttachLogic: {
        const bool targetPlayer  = (s.target.kind == TargetKind::Id  && s.target.value == "player");
        const bool targetHostile = (s.target.kind == TargetKind::Tag && s.target.value == "hostile");

        if (targetPlayer && !hasPlayer) {
          fail("attach logic to player but player is not created");
        }
        if (targetHostile && !hasHostiles) {
          fail("attach logic to hostile but hostile is not created");
        }

        if (s.logic.name == "contact_damage") {
          auto it = s.logic.params.find("damage");
          if (it == s.logic.params.end()) fail("contact_damage requires parameter: damage");
          else if (it->second <= 0) fail("contact_damage.damage must be > 0");
        }
        break;
      }

      case StmtKind::Spawn:
        if (!hasTerrain) fail("spawn requires terrain (create terrain WxH;)");
        if (!hasHostiles) fail("spawn hostile requires hostiles (create humanoid hostile ...;)");
        break;
    }
  }

  if (terrainCount > 1) fail("terrain can be created only once in v0.1");

  if (ok) {
    out << "OK: validation passed\n";
    if (hasTerrain) out << "INFO: terrain=" << terrainW << "x" << terrainH << "\n";
    if (hasPlayer) out << "INFO: player=created\n";
    if (hasHostiles) out << "INFO: hostile_count=" << hostileCount << "\n";
  }

  return ValidationResult{ok, out.str()};
}

} // namespace yuspec