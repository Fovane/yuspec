#pragma once
#include <string>
#include <vector>
#include <utility>

namespace yuspec {

// Minimal deterministic JSON writer.
// - Always uses stable key order (you control insertion order).
// - Escapes strings safely for v0.1 needs.
class JsonWriter {
public:
  void begin_object();
  void end_object();
  void begin_array();
  void end_array();

  void key(const std::string& k);

  void value_string(const std::string& s);
  void value_int(int64_t v);
  void value_bool(bool v);

  // Convenience: "key": "value"
  void kv_string(const std::string& k, const std::string& v);
  void kv_int(const std::string& k, int64_t v);
  void kv_bool(const std::string& k, bool v);

  std::string str() const { return out_; }

private:
  enum class Ctx { Obj, Arr };
  std::string out_;
  std::vector<Ctx> stack_;
  std::vector<bool> first_; // first element in current container?

  void comma_if_needed();
  void push(Ctx c);
  void pop(Ctx c);

  static std::string escape(const std::string& s);
};

} // namespace yuspec