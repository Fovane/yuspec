#include "yuspec_rt/world.h"
#include <cmath>

namespace yuspec_rt {

World::World(uint32_t seed) : seed_(seed), rng_(seed) {}

void World::create_terrain(int w, int h) {
  terrain_.w = w;
  terrain_.h = h;
}

Entity* World::player() {
  for (auto& e : entities_) {
    if (e.kind == EntityKind::Player) return &e;
  }
  return nullptr;
}

Entity& World::create_player(int health, int mana) {
  Entity e;
  e.id = next_id_++;
  e.kind = EntityKind::Player;
  e.tag = "player";
  e.stats.health = health;
  e.stats.mana = mana;
  e.pos = { terrain_.w / 2.0f, terrain_.h / 2.0f };
  entities_.push_back(e);
  return entities_.back();
}

void World::create_hostiles(int count, int health) {
  hostileCount_ = count;
  hostileHealth_ = health;
}

void World::attach_move_to_player() {
  if (auto* p = player()) p->hasMove = true;
}

void World::attach_chase_to_hostiles() {
  for (auto& e : entities_) {
    if (e.tag == "hostile") e.hasChase = true;
  }
  // Also applies to future hostiles; we’ll set on spawn in spawn_hostiles_random().
}

void World::attach_contact_damage_to_hostiles(int damage) {
  for (auto& e : entities_) {
    if (e.tag == "hostile") {
      e.hasContactDamage = true;
      e.contactDamage = damage;
    }
  }
  // Also applies to future hostiles; we’ll set on spawn in spawn_hostiles_random().
}

void World::spawn_hostiles_random() {
  // Spawn new hostile entities based on last create_hostiles call
  for (int i = 0; i < hostileCount_; i++) {
    Entity e;
    e.id = next_id_++;
    e.kind = EntityKind::Hostile;
    e.tag = "hostile";
    e.stats.health = hostileHealth_;
    e.stats.mana = 0;

    // random position in terrain bounds
    int x = rng_.next_int(0, std::max(0, terrain_.w - 1));
    int y = rng_.next_int(0, std::max(0, terrain_.h - 1));
    e.pos = { (float)x, (float)y };

    // Apply default hostile logics that may have been requested (v0.1 rule):
    // If any existing hostile has chase/contact_damage, assume those are "class-level" settings.
    bool wantChase = false;
    bool wantCD = false;
    int cd = 0;
    for (const auto& ex : entities_) {
      if (ex.tag == "hostile") {
        wantChase = wantChase || ex.hasChase;
        wantCD = wantCD || ex.hasContactDamage;
        if (ex.hasContactDamage) cd = ex.contactDamage;
      }
    }
    // If no hostiles exist yet, we default to false; v0.1 scripts typically attach after create_hostiles but before spawn.
    // We'll also handle attachments later by letting executor attach after spawn, but current compiler order attaches before spawn.
    e.hasChase = wantChase;
    e.hasContactDamage = wantCD;
    e.contactDamage = cd;

    entities_.push_back(e);
  }
}

void World::clamp_to_terrain(Entity& e) {
  if (terrain_.w <= 0 || terrain_.h <= 0) return;
  if (e.pos.x < 0) e.pos.x = 0;
  if (e.pos.y < 0) e.pos.y = 0;
  if (e.pos.x > terrain_.w - 1) e.pos.x = (float)(terrain_.w - 1);
  if (e.pos.y > terrain_.h - 1) e.pos.y = (float)(terrain_.h - 1);
}

void World::tick(float dt) {
  // 1) Reset velocities
  for (auto& e : entities_) e.vel = {0, 0};

    // 2a) Apply move (player)
  if (auto* p2 = player()) {
    if (p2->hasMove) {
      float dx = playerMoveDir_.x;
      float dy = playerMoveDir_.y;
      float len = std::sqrt(dx*dx + dy*dy);
      if (len > 0.0001f) {
        dx /= len; dy /= len;
        float speed = 10.0f; // player speed
        p2->vel.x += dx * speed;
        p2->vel.y += dy * speed;
      }
    }
  }

  // 3) Apply chase (hostiles)
  Entity* p = player();
  if (p) {
    for (auto& e : entities_) {
      if (e.tag != "hostile") continue;
      if (!e.hasChase) continue;

      float dx = p->pos.x - e.pos.x;
      float dy = p->pos.y - e.pos.y;
      float len = std::sqrt(dx*dx + dy*dy);
      if (len > 0.0001f) {
        dx /= len; dy /= len;
        // chase speed
        float speed = 6.0f;
        e.vel.x += dx * speed;
        e.vel.y += dy * speed;
      }
    }
  }

  // 4) Integrate positions
  for (auto& e : entities_) {
    e.pos.x += e.vel.x * dt;
    e.pos.y += e.vel.y * dt;
    clamp_to_terrain(e);
  }

  // 5) Contact damage
  if (p && p->stats.health > 0) {
    for (auto& e : entities_) {
      if (e.tag != "hostile") continue;
      if (!e.hasContactDamage) continue;
      if (e.contactDamage <= 0) continue;

      float dx = p->pos.x - e.pos.x;
      float dy = p->pos.y - e.pos.y;
      float dist2 = dx*dx + dy*dy;

      // contact radius (simple)
      if (dist2 <= 0.8f * 0.8f) {
        p->stats.health -= e.contactDamage;
        if (p->stats.health < 0) p->stats.health = 0;
      }
    }
  }
}

void World::set_player_move_dir(float x, float y) {
  playerMoveDir_.x = x;
  playerMoveDir_.y = y;
}

} // namespace yuspec_rt