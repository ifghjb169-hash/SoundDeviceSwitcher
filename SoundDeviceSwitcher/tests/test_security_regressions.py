"""Offline source guardrails; these do not replace a Windows build or native DLL tests."""
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class SecurityRegressionTests(unittest.TestCase):
    def test_voicemeeter_native_dependencies_use_restricted_search(self):
        code = (ROOT / "src" / "VoicemeeterRemoteClient.cs").read_text(encoding="utf-8")
        self.assertIn('EntryPoint = "LoadLibraryExW"', code)
        self.assertIn("LoadLibrarySearchDllLoadDir | LoadLibrarySearchSystem32", code)
        self.assertNotRegex(code, r"NativeMethods\.LoadLibrary\(")

    def test_only_installed_voicemeeter_dll_is_allowed(self):
        code = (ROOT / "src" / "VoicemeeterRemoteClient.cs").read_text(encoding="utf-8")
        lookup = code.split("private static string FindVoicemeeterRemoteDll()", 1)[1].split(
            "private static string DriverFromType", 1
        )[0]
        self.assertIn("Environment.SpecialFolder.ProgramFilesX86", lookup)
        self.assertIn("Environment.SpecialFolder.ProgramFiles", lookup)
        self.assertIn('Path.Combine(vendorDirectory, "Voicemeeter")', lookup)
        self.assertIn('Path.Combine(installDirectory, "VoicemeeterRemote64.dll")', lookup)
        self.assertNotIn("AppDomain.CurrentDomain.BaseDirectory", lookup)
        self.assertNotIn("Environment.CurrentDirectory", lookup)
        self.assertNotIn("GetEnvironmentVariable", lookup)
        self.assertNotIn('Path.Combine(baseDir, "VoicemeeterRemote64.dll")', code)

    def test_reparse_point_redirects_are_rejected(self):
        code = (ROOT / "src" / "VoicemeeterRemoteClient.cs").read_text(encoding="utf-8")
        lookup = code.split("private static string FindVoicemeeterRemoteDll()", 1)[1].split(
            "private static string DriverFromType", 1
        )[0]
        self.assertIn("FileAttributes.ReparsePoint", lookup)
        self.assertIn("throw new InvalidOperationException", lookup)
        for path in ("programFilesDirectory", "vendorDirectory", "installDirectory", "dllPath"):
            self.assertIn(path, lookup)

    def test_load_failure_unloads_library(self):
        code = (ROOT / "src" / "VoicemeeterRemoteClient.cs").read_text(encoding="utf-8")
        self.assertRegex(code, r"catch\s*\{\s*NativeMethods\.FreeLibrary\(_library\);\s*_library = IntPtr.Zero;")

    def test_checkout_does_not_persist_git_credentials(self):
        for name in ("codeql.yml", "release.yml"):
            with self.subTest(name=name):
                workflow = (ROOT / ".github" / "workflows" / name).read_text(encoding="utf-8")
                self.assertRegex(workflow, r"uses: actions/checkout@v4\s+with:\s+persist-credentials: false")

    def test_release_keeps_build_provenance(self):
        workflow = (ROOT / ".github" / "workflows" / "release.yml").read_text(encoding="utf-8")
        self.assertIn("Attest Build Provenance", workflow)
        self.assertIn("SHA256SUMS.txt", workflow)

    def test_secret_patterns_are_ignored(self):
        ignore = (ROOT / ".gitignore").read_text(encoding="utf-8")
        self.assertRegex(ignore, r"(?m)^\.env$")
        self.assertRegex(ignore, r"(?m)^\.env\.\*$")
        self.assertIn("*.pfx", ignore)


if __name__ == "__main__":
    unittest.main()
