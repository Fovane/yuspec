#include "yuspec/json_writer.h"
#include <sstream>

namespace yuspec {

void JsonWriter::push(Ctx c) { stack_.push_back(c); first_.push_back(true); }
void JsonWriter::pop(Ctx c) {
  if (stack_.empty() || stack_.back() != c) return; // v0.1: keep simple
  stack_.pop_back();
  first_.pop_back();
}

void JsonWriter::comma_if_needed() {
  if (stack_.empty()) return;
  if (!first_.back()) out_ += ",";
  first_.back() = false;
}

std::string JsonWriter::escape(const std::string& s) {
  std::ostringstream oss;
  for (char c : s) {
    switch (c) {
      case '\\': oss << "\\\\"; break;
      case '"':  oss << "\\\""; break;
      case '\n': oss << "\\n"; break;
      case '\r': oss << "\\r"; break;
      case '\t': oss << "\\t"; break;
      default: oss << c; break;
    }
  }
  return oss.str();
}

void JsonWriter::begin_object() { comma_if_needed(); out_ += "{"; push(Ctx::Obj); }
void JsonWriter::end_object()   { out_ += "}"; pop(Ctx::Obj); }

void JsonWriter::begin_array()  { comma_if_needed(); out_ += "["; push(Ctx::Arr); }
void JsonWriter::end_array()    { out_ += "]"; pop(Ctx::Arr); }

void JsonWriter::key(const std::string& k) {
  // Keys only valid inside object; keep v0.1 simple.
  comma_if_needed();
  out_ += "\"";
  out_ += escape(k);
  out_ += "\":";
  // After key, next value should not emit comma again:
  // We'll temporarily mark "first_" as true so value doesn't add comma incorrectly.
  // Easiest: push a fake state? Instead, we just set first_.back()=true then value handles comma_if_needed.
  // But value uses comma_if_needed, which would add comma. So for "key:value", we must bypass comma in value.
  // We solve by writing value methods without comma_if_needed when immediately after key.
  // We'll implement kv_* methods for objects; and keep key()+value_* only for arrays / manual use.
}

void JsonWriter::value_string(const std::string& s) { comma_if_needed(); out_ += "\""+escape(s)+"\""; }
void JsonWriter::value_int(int64_t v) { comma_if_needed(); out_ += std::to_string(v); }
void JsonWriter::value_bool(bool v) { comma_if_needed(); out_ += (v ? "true" : "false"); }

void JsonWriter::kv_string(const std::string& k, const std::string& v) {
  comma_if_needed();
  out_ += "\""+escape(k)+"\":";
  out_ += "\""+escape(v)+"\"";
}
void JsonWriter::kv_int(const std::string& k, int64_t v) {
  comma_if_needed();
  out_ += "\""+escape(k)+"\":";
  out_ += std::to_string(v);
}
void JsonWriter::kv_bool(const std::string& k, bool v) {
  comma_if_needed();
  out_ += "\""+escape(k)+"\":";
  out_ += (v ? "true" : "false");
}

} // namespace yuspec