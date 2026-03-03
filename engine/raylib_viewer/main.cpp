#include <iostream>
#include <fstream>
#include <sstream>
#include <string>

#include "raylib.h"

#include "yuspec/lexer.h"
#include "yuspec/parser.h"
#include "yuspec/validate.h"
#include "yuspec_rt/world.h"

static std::string read_all(const std::string& path) {
  std::ifstream in(path, std::ios::binary);
  if (!in) throw std::runtime_error("Failed to open file: " + path);
  std::ostringstream ss;
  ss << in.rdbuf();
  return ss.str();
}

static void execute_stmt(yuspec_rt::World& w, const yuspec::Stmt& s) {
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
        // mark "class-level" intention, apply after spawn too
        for (auto& e : w.entities_mut()) if (e.tag == "hostile") e.hasChase = true;
      } else if (s.logic.name == "contact_damage" && s.target.value == "hostile") {
        int dmg = 0;
        auto it = s.logic.params.find("damage");
        if (it != s.logic.params.end()) dmg = it->second;
        for (auto& e : w.entities_mut()) if (e.tag == "hostile") { e.hasContactDamage = true; e.contactDamage = dmg; }
      }
      break;
    case StmtKind::Spawn:
      w.spawn_hostiles_random();
      break;
  }
}

int main(int argc, char** argv) {
  try {
    if (argc < 2) {
      std::cerr << "Usage: yuspec_view <file.yus>\n";
      return 1;
    }

    std::string path = argv[1];
    std::string src = read_all(path);

    yuspec::Lexer lex(src);
    yuspec::Parser parser(lex);
    yuspec::Program prog = parser.parse_program();

    auto v = yuspec::validate_v01(prog);
    if (!v.ok) {
      std::cerr << v.report;
      return 2;
    }

    // Build world
    yuspec_rt::World world(12345);
    for (const auto& st : prog.statements) execute_stmt(world, st);

    // Apply hostile logic after spawn (because attach happens before spawn in scripts)
    bool wantsChase = false, wantsCD = false;
    int cd = 0;
    for (const auto& st : prog.statements) {
      if (st.kind == yuspec::StmtKind::AttachLogic && st.target.value == "hostile") {
        if (st.logic.name == "chase") wantsChase = true;
        if (st.logic.name == "contact_damage") {
          wantsCD = true;
          auto it = st.logic.params.find("damage");
          if (it != st.logic.params.end()) cd = it->second;
        }
      }
    }
    for (auto& e : world.entities_mut()) {
      if (e.tag != "hostile") continue;
      if (wantsChase) e.hasChase = true;
      if (wantsCD) { e.hasContactDamage = true; e.contactDamage = cd; }
    }

    // Window
    const int screenW = 1000;
    const int screenH = 800;
    InitWindow(screenW, screenH, "Yuspec Raylib Demo");
    SetTargetFPS(60);

    // Camera/scale: terrain coords -> pixels
    const auto& t = world.terrain();
    float scale = 6.0f; // pixels per tile
    Vector2 origin = { 50.0f, 50.0f };

    while (!WindowShouldClose()) {
      // Input (WASD)
      float mx = 0.0f, my = 0.0f;
      if (IsKeyDown(KEY_A)) mx -= 1.0f;
      if (IsKeyDown(KEY_D)) mx += 1.0f;
      if (IsKeyDown(KEY_W)) my -= 1.0f;
      if (IsKeyDown(KEY_S)) my += 1.0f;
      world.set_player_move_dir(mx, my);

      // Sim step
      float dt = GetFrameTime();
      world.tick(dt);

      BeginDrawing();
      ClearBackground(RAYWHITE);

      // Terrain border
      DrawRectangleLines((int)origin.x, (int)origin.y, (int)(t.w * scale), (int)(t.h * scale), DARKGRAY);

      // Entities
      for (const auto& e : world.entities()) {
        int px = (int)(origin.x + e.pos.x * scale);
        int py = (int)(origin.y + e.pos.y * scale);

        if (e.tag == "player") {
          DrawRectangle(px - 6, py - 6, 12, 12, BLUE);
          DrawText(TextFormat("HP: %d", e.stats.health), 20, 20, 20, BLACK);
        } else if (e.tag == "hostile") {
          DrawRectangle(px - 5, py - 5, 10, 10, RED);
        }
      }

      DrawText("WASD to move. Hostiles chase. Contact damage reduces HP.", 20, screenH - 30, 18, DARKGRAY);

      EndDrawing();
    }

    CloseWindow();
    return 0;

  } catch (const std::exception& e) {
    std::cerr << "Error: " << e.what() << "\n";
    return 3;
  }
}