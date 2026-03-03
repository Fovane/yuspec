#pragma once
#include <string>
#include <vector>
#include <memory>

namespace yuspec_rt {

struct Vec2 {
  float x = 0.0f;
  float y = 0.0f;
};

enum class EntityKind { Player, Hostile };

struct Stats {
  int health = 100;
  int mana = 0;
};

struct Entity {
  int id = 0;
  EntityKind kind{};
  std::string tag;     // "hostile" etc.
  Vec2 pos{};
  Vec2 vel{};
  Stats stats{};

  // runtime attached logic flags/params (v0.1)
  bool hasMove = false;
  bool hasChase = false;
  bool hasContactDamage = false;
  int contactDamage = 0;
};

} // namespace yuspec_rt