#pragma once
#include <vector>
#include <optional>
#include "yuspec_rt/entity.h"
#include "yuspec_rt/rng.h"

namespace yuspec_rt {

struct Terrain {
  int w = 0;
  int h = 0;
};

class World {
public:
  explicit World(uint32_t seed);

  void set_player_move_dir(float x, float y);

  void create_terrain(int w, int h);

  Entity& create_player(int health, int mana);
  void create_hostiles(int count, int health);

  // Logic attachment (v0.1)
  void attach_move_to_player();
  void attach_chase_to_hostiles();
  void attach_contact_damage_to_hostiles(int damage);

  void spawn_hostiles_random();

  // Simulation
  void tick(float dt);

  // Access
  const Terrain& terrain() const { return terrain_; }
  const std::vector<Entity>& entities() const { return entities_; }
  std::vector<Entity>& entities_mut() { return entities_; }
  Entity* player();

private:
  uint32_t seed_ = 0;
  Rng rng_;
  Terrain terrain_{};
  std::vector<Entity> entities_;
  int next_id_ = 1;

  Vec2 playerMoveDir_{0,0};

  // Remember "definitions" for hostiles before spawn
  int hostileCount_ = 0;
  int hostileHealth_ = 30;

  void clamp_to_terrain(Entity& e);
};

} // namespace yuspec_rt