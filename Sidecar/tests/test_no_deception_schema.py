"""Keep diagnostic truth or deception claims out of client-facing schemas."""

import ast
from pathlib import Path
import re
import unittest

from prosody import ProsodySignal


FORBIDDEN_TOKENS = frozenset(
    {
        "deception",
        "deceptive",
        "guilt",
        "guilty",
        "intent",
        "lie",
        "lies",
        "liar",
        "lying",
        "truth",
        "truthfulness",
    }
)


def _key_tokens(key: str) -> set[str]:
    separated = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", key)
    return set(re.findall(r"[a-z0-9]+", separated.lower()))


def assert_no_deception_keys(keys: list[str]) -> None:
    violations = {
        key: sorted(_key_tokens(key) & FORBIDDEN_TOKENS)
        for key in keys
        if _key_tokens(key) & FORBIDDEN_TOKENS
    }
    if violations:
        raise AssertionError(f"Diagnostic claim keys are not allowed: {violations}")


def _empty_turn_response_keys() -> list[str]:
    app_path = Path(__file__).resolve().parents[1] / "app.py"
    module = ast.parse(app_path.read_text(encoding="utf-8"))
    function = next(
        node
        for node in module.body
        if isinstance(node, ast.FunctionDef) and node.name == "_empty_response"
    )
    response = next(
        node.value
        for node in function.body
        if isinstance(node, ast.Return) and isinstance(node.value, ast.Dict)
    )
    return [key.value for key in response.keys if isinstance(key, ast.Constant) and isinstance(key.value, str)]


def _unity_dto_fields() -> list[str]:
    dto_path = Path(__file__).resolve().parents[2] / "Assets" / "_Project" / "Scripts" / "Net" / "SidecarDtos.cs"
    source = dto_path.read_text(encoding="utf-8")
    return re.findall(r"public\s+\S+\s+(\w+)\s*;", source)


class NoDeceptionSchemaTests(unittest.TestCase):
    def test_guard_rejects_diagnostic_keys_in_snake_or_camel_case(self):
        with self.assertRaisesRegex(AssertionError, "lie_probability"):
            assert_no_deception_keys(["lie_probability"])
        with self.assertRaisesRegex(AssertionError, "isLying"):
            assert_no_deception_keys(["isLying"])

    def test_current_python_and_unity_payload_keys_are_non_diagnostic(self):
        assert_no_deception_keys(list(ProsodySignal().to_dict()))
        assert_no_deception_keys(_empty_turn_response_keys())
        assert_no_deception_keys(_unity_dto_fields())


if __name__ == "__main__":
    unittest.main()
