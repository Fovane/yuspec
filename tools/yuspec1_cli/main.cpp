// YUSPEC v1.0 — CLI Entry Point
// Entity-Behavior Programming — compile, validate, run, test
#include <fstream>
#include <iostream>
#include <sstream>
#include <string>

#include "yuspec/v1_lexer.h"
#include "yuspec/v1_parser.h"
#include "yuspec/v1_sema.h"
#include "yuspec_rt/v1_interpreter.h"

static std::string read_file(const std::string& path) {
  std::ifstream f(path, std::ios::binary);
  if (!f) { std::cerr << "Cannot open: " << path << "\n"; std::exit(1); }
  std::ostringstream ss; ss << f.rdbuf(); return ss.str();
}

static void usage() {
  std::cerr << R"(
YUSPEC v1.0 — Entity-Behavior Programming
Usage:
  yuspec1 validate  <file.yus>
  yuspec1 run       <file.yus> [--zone NAME] [--scenario NAME]
  yuspec1 test      <file.yus> [--verbose]
  yuspec1 tokens    <file.yus>
  yuspec1 parse     <file.yus>
  yuspec1 run-all-scenarios <file.yus>

Options:
  --verbose        Print tick trace and event log
  --ticks N        Max simulation ticks (default: 10000)
  --tick-ms N      Milliseconds per tick (default: 16)
  --zone NAME      Run a specific zone
  --scenario NAME  Run a specific scenario
)";
}

static bool compile(const std::string& path, const std::string& src,
                    yuspec::v1::Program& out_prog) {
  yuspec::v1::Lexer lexer(src, path);
  yuspec::v1::Parser parser(lexer);
  out_prog = parser.parse_program();

  bool ok = true;
  for (auto& d : parser.diagnostics()) {
    std::cerr << (d.level == yuspec::v1::Diag::Error ? "ERROR" : "WARN")
              << " " << path << ":" << d.pos.line << ":" << d.pos.col
              << ": " << d.message << "\n";
    if (d.level == yuspec::v1::Diag::Error) ok = false;
  }
  return ok;
}

int main(int argc, char** argv) {
  if (argc < 3) { usage(); return 1; }

  std::string cmd    = argv[1];
  std::string inpath = argv[2];

  // Parse options
  bool verbose    = false;
  int max_ticks   = 10000;
  double tick_ms  = 16.0;
  std::string zone_name;
  std::string scenario_name;

  for (int i = 3; i < argc; i++) {
    std::string a = argv[i];
    if (a == "--verbose")           { verbose = true; continue; }
    if (a == "--ticks"   && i+1<argc) { max_ticks  = std::stoi(argv[++i]); continue; }
    if (a == "--tick-ms" && i+1<argc) { tick_ms    = std::stod(argv[++i]); continue; }
    if (a == "--zone"    && i+1<argc) { zone_name  = argv[++i]; continue; }
    if (a == "--scenario"&& i+1<argc) { scenario_name = argv[++i]; continue; }
  }

  std::string src = read_file(inpath);

  // ── tokens ──────────────────────────────────────────────────────────────
  if (cmd == "tokens") {
    yuspec::v1::Lexer lexer(src, inpath);
    while (true) {
      auto tok = lexer.next();
      std::cout << yuspec::v1::tk_name(tok.kind) << "\t'"
                << tok.lexeme << "'\tline=" << tok.pos.line << "\n";
      if (tok.kind == yuspec::v1::TK::END_OF_FILE) break;
    }
    return 0;
  }

  // ── parse (print summary) ────────────────────────────────────────────────
  if (cmd == "parse") {
    yuspec::v1::Program prog;
    bool ok = compile(inpath, src, prog);
    std::cout << "Declarations: " << prog.declarations.size() << "\n";
    for (auto& d : prog.declarations) {
      if (!d) continue;
      if (d->is<yuspec::v1::EntityDecl>())      std::cout << "  entity       " << d->as<yuspec::v1::EntityDecl>().name << "\n";
      if (d->is<yuspec::v1::BehaviorDecl>())    std::cout << "  behavior     " << d->as<yuspec::v1::BehaviorDecl>().name << "\n";
      if (d->is<yuspec::v1::EventDecl>())       std::cout << "  event        " << d->as<yuspec::v1::EventDecl>().name << "\n";
      if (d->is<yuspec::v1::WorkflowDecl>())    std::cout << "  workflow     " << d->as<yuspec::v1::WorkflowDecl>().name << "\n";
      if (d->is<yuspec::v1::ZoneDecl>())        std::cout << "  zone         " << d->as<yuspec::v1::ZoneDecl>().name << "\n";
      if (d->is<yuspec::v1::SystemDecl>())      std::cout << "  system       " << d->as<yuspec::v1::SystemDecl>().name << "\n";
      if (d->is<yuspec::v1::ScenarioDecl>())    std::cout << "  scenario     " << d->as<yuspec::v1::ScenarioDecl>().name << "\n";
      if (d->is<yuspec::v1::StateMachineDecl>()) std::cout << "  statemachine " << d->as<yuspec::v1::StateMachineDecl>().name << "\n";
      if (d->is<yuspec::v1::ComponentDecl>())   std::cout << "  component    " << d->as<yuspec::v1::ComponentDecl>().name << "\n";
    }
    return ok ? 0 : 2;
  }

  // ── validate ─────────────────────────────────────────────────────────────
  if (cmd == "validate") {
    yuspec::v1::Program prog;
    bool parse_ok = compile(inpath, src, prog);
    if (!parse_ok) return 2;

    yuspec::v1::Sema sema;
    auto result = sema.analyze(prog);
    std::cout << result.report();
    return result.ok ? 0 : 4;
  }

  // ── run ──────────────────────────────────────────────────────────────────
  if (cmd == "run") {
    yuspec::v1::Program prog;
    if (!compile(inpath, src, prog)) return 2;

    yuspec::v1::rt::Interpreter interp;
    interp.load_program(prog);

    yuspec::v1::rt::RunConfig cfg;
    cfg.max_ticks = max_ticks;
    cfg.tick_ms   = tick_ms;
    cfg.verbose   = verbose;
    cfg.record_events = verbose;

    yuspec::v1::rt::RunResult result;
    if (!scenario_name.empty()) {
      result = interp.run_scenario(scenario_name, cfg);
    } else if (!zone_name.empty()) {
      result = interp.run_zone(zone_name, cfg);
    } else {
      // Run first zone or scenario found
      yuspec::v1::Lexer lex2(src); yuspec::v1::Parser p2(lex2);
      auto prog2 = p2.parse_program();
      std::string first_scenario, first_zone;
      for (auto& d : prog2.declarations) {
        if (!d) continue;
        if (d->is<yuspec::v1::ScenarioDecl>() && first_scenario.empty())
          first_scenario = d->as<yuspec::v1::ScenarioDecl>().name;
        if (d->is<yuspec::v1::ZoneDecl>() && first_zone.empty())
          first_zone = d->as<yuspec::v1::ZoneDecl>().name;
      }
      if (!first_scenario.empty()) result = interp.run_scenario(first_scenario, cfg);
      else if (!first_zone.empty()) result = interp.run_zone(first_zone, cfg);
      else { std::cerr << "Nothing to run. Define a scenario or zone.\n"; return 1; }
    }

    std::cout << result.output;
    if (!result.errors.empty()) std::cerr << result.errors << "\n";
    return result.ok ? 0 : 1;
  }

  // ── test ─────────────────────────────────────────────────────────────────
  if (cmd == "test" || cmd == "run-all-scenarios") {
    yuspec::v1::Program prog;
    if (!compile(inpath, src, prog)) return 2;

    yuspec::v1::rt::Interpreter interp;
    interp.load_program(prog);

    yuspec::v1::rt::RunConfig cfg;
    cfg.max_ticks = max_ticks;
    cfg.tick_ms   = tick_ms;
    cfg.verbose   = verbose;
    cfg.record_events = true;

    auto result = interp.run_all_scenarios(cfg);
    std::cout << result.output;
    std::cout << "\nTotal: " << result.assertions_passed << " passed, "
              << result.assertions_failed << " failed\n";
    return result.ok ? 0 : 1;
  }

  usage(); return 1;
}
