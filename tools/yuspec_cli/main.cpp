#include <fstream>
#include <iostream>
#include <sstream>

#include "yuspec/lexer.h"
#include "yuspec/parser.h"
#include "yuspec/ir.h"
#include "yuspec/diagnostic.h"
#include "yuspec/validate.h"
#include "yuspec_rt/runtime.h"

static std::string read_all(const std::string& path) {
  std::ifstream in(path, std::ios::binary);
  if (!in) throw std::runtime_error("Failed to open file: " + path);
  std::ostringstream ss;
  ss << in.rdbuf();
  return ss.str();
}

static void write_all(const std::string& path, const std::string& content) {
  std::ofstream out(path, std::ios::binary);
  if (!out) throw std::runtime_error("Failed to write file: " + path);
  out << content;
}

static void usage() {
  std::cerr <<
    "Yuspec v0.1\n"
    "Usage:\n"
    "  yuspec trace <file.yus>\n"
    "  yuspec compile <file.yus> -o <out.json>\n"
    "  yuspec validate <file.yus>\n"
    "  yuspec run <file.yus> [--ticks N] [--dt X] [--verbose]\n";
}

int main(int argc, char** argv) {
  try {
    if (argc < 3) { usage(); return 1; }

    std::string cmd = argv[1];
    std::string inPath = argv[2];

    std::string src = read_all(inPath);
    yuspec::Lexer lex(src);
    yuspec::Parser parser(lex);
    yuspec::Program prog = parser.parse_program();

    if (cmd == "trace") {
      std::cout << yuspec::build_trace(prog);
      return 0;
    }

    if (cmd == "compile") {
      std::string outPath;
      for (int i = 3; i < argc; i++) {
        std::string a = argv[i];
        if (a == "-o" && i + 1 < argc) {
          outPath = argv[i + 1];
          i++;
        }
      }
      if (outPath.empty()) {
        usage();
        return 1;
      }

      yuspec::IR ir = yuspec::build_ir_v01(prog);
      write_all(outPath, ir.json);
      std::cout << "Wrote IR: " << outPath << "\n";
      return 0;
    }

    if (cmd == "validate") {
      auto res = yuspec::validate_v01(prog);
      std::cout << res.report;
      return res.ok ? 0 : 4;
    }

    if (cmd == "run") {
  yuspec_rt::RunConfig cfg;

  for (int i = 3; i < argc; i++) {
    std::string a = argv[i];
    if (a == "--ticks" && i + 1 < argc) { cfg.ticks = std::stoi(argv[++i]); continue; }
    if (a == "--dt" && i + 1 < argc) { cfg.dt = std::stof(argv[++i]); continue; }
    if (a == "--verbose") { cfg.verbose = true; continue; }
  }

  auto res = yuspec_rt::run_program_v01(prog, cfg);
  std::cout << res.report;
  return 0;
  }

    usage();
    return 1;
  } catch (const yuspec::CompileError& e) {
    std::cerr << e.what() << "\n";
    return 2;
  } catch (const std::exception& e) {
    std::cerr << "Error: " << e.what() << "\n";
    return 3;
  }
}