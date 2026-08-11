from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PROVIDER = ROOT / "Rimtalk-Persona-Director" / "Assemblies" / "DirectorVariableProvider.cs"
RIMTALK_PARSER = ROOT.parent / "RimTalk" / "Source" / "Prompt" / "Parser" / "ScribanParser.cs"


def test_legacy_patch_is_skipped_for_the_current_scriban_contract():
    provider = PROVIDER.read_text(encoding="utf-8-sig")
    parser = RIMTALK_PARSER.read_text(encoding="utf-8-sig")

    assert "[HarmonyPrepare]" in provider
    assert "ResolveLegacyTarget() != null" in provider
    assert "ResolveCurrentRenderTarget() != null" in provider
    assert 'CurrentParserTypeName = "RimTalk.Prompt.ScribanParser"' in provider
    assert 'CurrentContextTypeName = "RimTalk.Prompt.PromptContext"' in provider
    assert 'AccessTools.Method(parserType, "Render", new Type[3]' in provider
    assert "typeof(string)" in provider
    assert "contextType" in provider
    assert "typeof(bool)" in provider
    assert "public static string Render(string templateText, PromptContext context, bool logErrors = true)" in parser


def test_unsupported_prompt_api_reports_a_clear_contract_error():
    provider = PROVIDER.read_text(encoding="utf-8-sig")

    assert "Unsupported RimTalk prompt API" in provider
    assert "MustacheParser.EvaluateExpression(string, MustacheContext)" in provider
    assert "ScribanParser.Render(string, PromptContext, bool)" in provider
