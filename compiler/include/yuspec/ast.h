#pragma once
#include <string>
#include <unordered_map>
#include <vector>

namespace yuspec {

enum class StmtKind {
  CreateTerrain,
  CreatePlayer,
  CreateHostiles,
  AttachLogic,
  Spawn,
};

enum class TargetKind { Id, Tag };

struct TargetRef {
  TargetKind kind{};
  std::string value; // "player" or "hostile"
};

struct LogicSpec {
  std::string name; // "move" | "chase" | "contact_damage"
  // Keep params small for v0.1; later can be typed values.
  std::unordered_map<std::string, int> params;
};

struct Stmt {
  StmtKind kind{};

  // CreateTerrain
  int w = 0, h = 0;

  // CreatePlayer
  int playerHealth = 0;
  int playerMana = 0;

  // CreateHostiles
  int hostileCount = 0;
  int hostileHealth = 0;

  // AttachLogic
  TargetRef target{};
  LogicSpec logic{};

  // Spawn
  TargetRef spawnTarget{};
};

struct Program {
  std::vector<Stmt> statements;
};

} // namespace yuspec