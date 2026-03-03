#include "yuspec/ir.h"
#include "yuspec/json_writer.h"
#include <sstream>
#include <algorithm>

namespace yuspec {

IR build_ir_v01(const Program& program) {
  // statements // Can't. We'll implement statements with kv pattern by manual: add helper in writer? 
  // To keep compilation simple, we'll avoid direct access.
  // We'll write "statements":[...] using kv_* + begin_array manually by writing raw fragments with safe method?
  // Simplest: extend JsonWriter with a public raw() or fix writer.
  // We'll do a minimal safe workaround here by building JSON with jw methods only:
  // We'll implement by writing keys as strings using kv_* isn't enough for arrays. So we need "key_array" method.
  // Instead of editing writer privately here, we keep ir.cpp minimal by constructing with std::string.

  // ---- Minimal deterministic JSON without overengineering ----
  // We'll rebuild quickly with a local stringstream deterministic writer for v0.1:
  std::ostringstream oss;
  oss << "{";
  oss << "\"version\":\"0.1\",";
  oss << "\"statements\":[";
  for (size_t i = 0; i < program.statements.size(); i++) {
    const auto& s = program.statements[i];
    if (i) oss << ",";
    switch (s.kind) {
      case StmtKind::CreateTerrain:
        oss << "{\"op\":\"CreateTerrain\",\"w\":" << s.w << ",\"h\":" << s.h << "}";
        break;
      case StmtKind::CreatePlayer:
        oss << "{\"op\":\"CreateEntity\",\"entityType\":\"humanoid\",\"id\":\"player\","
            << "\"props\":{\"health\":" << s.playerHealth << ",\"mana\":" << s.playerMana << "}"
            << "}";
        break;
      case StmtKind::CreateHostiles:
        oss << "{\"op\":\"CreateEntities\",\"entityType\":\"humanoid\",\"tag\":\"hostile\","
            << "\"count\":" << s.hostileCount << ",\"props\":{\"health\":" << s.hostileHealth << "}"
            << "}";
        break;
      case StmtKind::AttachLogic: {
        oss << "{\"op\":\"AttachLogic\",";
        // target
        oss << "\"target\":{";
        oss << "\"kind\":\"" << ((s.target.kind == TargetKind::Id) ? "id" : "tag") << "\",";
        oss << "\"value\":\"" << s.target.value << "\"";
        oss << "},";
        // logic
        oss << "\"logic\":{";
        oss << "\"name\":\"" << s.logic.name << "\",";
        oss << "\"params\":{";
        // deterministic param order: sort keys
        std::vector<std::pair<std::string,int>> params(s.logic.params.begin(), s.logic.params.end());
        std::sort(params.begin(), params.end(), [](auto& a, auto& b){ return a.first < b.first; });
        for (size_t pi=0; pi<params.size(); pi++) {
          if (pi) oss << ",";
          oss << "\"" << params[pi].first << "\":" << params[pi].second;
        }
        oss << "}";
        oss << "}";
        oss << "}";
        break;
      }
      case StmtKind::Spawn:
        oss << "{\"op\":\"Spawn\","
            << "\"target\":{\"kind\":\"tag\",\"value\":\"hostile\"},"
            << "\"placement\":{\"kind\":\"random_in_terrain\"}"
            << "}";
        break;
    }
  }
  oss << "]}";

  IR ir;
  ir.json = oss.str();
  return ir;
}

std::string build_trace(const Program& program) {
  std::ostringstream oss;
  for (const auto& s : program.statements) {
    switch (s.kind) {
      case StmtKind::CreateTerrain:
        oss << "CreateTerrain " << s.w << "x" << s.h << "\n";
        break;
      case StmtKind::CreatePlayer:
        oss << "CreatePlayer hp=" << s.playerHealth << " mana=" << s.playerMana << "\n";
        break;
      case StmtKind::CreateHostiles:
        oss << "CreateHostiles count=" << s.hostileCount << " hp=" << s.hostileHealth << "\n";
        break;
      case StmtKind::AttachLogic:
        oss << "AttachLogic target=" << s.target.value << " logic=" << s.logic.name;
        if (!s.logic.params.empty()) {
          oss << " params={";
          bool first = true;
          // deterministic order
          std::vector<std::pair<std::string,int>> params(s.logic.params.begin(), s.logic.params.end());
          std::sort(params.begin(), params.end(), [](auto& a, auto& b){ return a.first < b.first; });
          for (auto& kv : params) {
            if (!first) oss << ",";
            first = false;
            oss << kv.first << ":" << kv.second;
          }
          oss << "}";
        }
        oss << "\n";
        break;
      case StmtKind::Spawn:
        oss << "Spawn target=hostile placement=random_in_terrain\n";
        break;
    }
  }
  return oss.str();
}

} // namespace yuspec