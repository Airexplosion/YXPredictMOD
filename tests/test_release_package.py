"""Regressions for the real release package and manager catalog contract."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest
import warnings
import zipfile


ROOT = Path(__file__).resolve().parent.parent
SPEC = importlib.util.spec_from_file_location("verify_release", ROOT / "scripts/verify-release.py")
RELEASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RELEASE)


class ReleasePackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.meta = RELEASE.read_json(ROOT / ".github/releases/latest.json")
        for relative in (".github/releases/latest.json", self.meta["artifact"], self.meta["notes"],
                         "mods.json", "mod/manifest.json", "mod/README.md", "mod/assets/horse/NOTICE.md"):
            self.copy(relative)
        for icon in RELEASE.ICONS:
            self.copy("mod/assets/horse/" + icon + ".png")

    def copy(self, relative):
        target = self.root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / relative, target)

    def write_json(self, relative, value):
        (self.root / relative).write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")

    def rewrite_archive(self, change):
        path = self.root / self.meta["artifact"]
        with zipfile.ZipFile(path) as archive:
            entries = [(name, archive.read(name)) for name in archive.namelist()]
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", UserWarning)
            with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
                for name, data in change(entries):
                    archive.writestr(name, data)
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        self.meta["sha256"] = digest
        self.write_json(".github/releases/latest.json", self.meta)
        catalog = RELEASE.read_json(self.root / "mods.json")
        catalog["mods"][0]["sha256"] = digest
        self.write_json("mods.json", catalog)

    def test_current_eleven_file_package_with_icons_is_accepted(self):
        result = RELEASE.verify(self.root)
        self.assertEqual(result["tag"], self.meta["tag"])
        self.assertEqual(result["title"], self.meta["title"])
        self.assertEqual(result["name"], "local.yxp.predictionpoints-" + self.meta["version"] + ".zip")

    def test_missing_mechanical_icon_is_rejected_even_with_valid_checksum(self):
        self.rewrite_archive(lambda entries: [(name, data) for name, data in entries if not name.endswith("/机械马.png")])
        with self.assertRaisesRegex(ValueError, "ZIP contents"):
            RELEASE.verify(self.root)

    def test_duplicate_entry_is_rejected_even_with_valid_checksum(self):
        self.rewrite_archive(lambda entries: entries + [entries[0]])
        with self.assertRaisesRegex(ValueError, "duplicate ZIP"):
            RELEASE.verify(self.root)

    def test_tampered_package_checksum_is_rejected(self):
        self.meta["sha256"] = "0" * 64
        self.write_json(".github/releases/latest.json", self.meta)
        with self.assertRaisesRegex(ValueError, "Package checksum mismatch"):
            RELEASE.verify(self.root)

    def test_source_and_packaged_manifest_must_match(self):
        manifest = RELEASE.read_json(self.root / "mod/manifest.json")
        manifest["author"] = "different author"
        self.write_json("mod/manifest.json", manifest)
        with self.assertRaisesRegex(ValueError, "Source and packaged manifests differ"):
            RELEASE.verify(self.root)

    def test_catalog_version_download_and_checksum_cannot_drift(self):
        original = RELEASE.read_json(self.root / "mods.json")
        for key, value in (("version", "0.0.0"), ("download", "https://example.com/wrong.zip"), ("sha256", "0" * 64)):
            with self.subTest(key=key):
                catalog = json.loads(json.dumps(original))
                catalog["mods"][0][key] = value
                self.write_json("mods.json", catalog)
                with self.assertRaisesRegex(ValueError, "Catalog|catalog"):
                    RELEASE.verify(self.root)

    def test_release_file_cannot_escape_repository(self):
        self.meta["artifact"] = "../outside.zip"
        self.write_json(".github/releases/latest.json", self.meta)
        with self.assertRaisesRegex(ValueError, "escapes repository"):
            RELEASE.verify(self.root)

    def test_stale_packaged_readme_is_rejected(self):
        (self.root / "mod/README.md").write_text("newer instructions", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Stale packaged file"):
            RELEASE.verify(self.root)

    def test_unexpected_user_data_cannot_enter_release(self):
        self.rewrite_archive(lambda entries: entries + [(RELEASE.MOD_ID + "/data/book.json", b"{}")])
        with self.assertRaisesRegex(ValueError, "ZIP contents"):
            RELEASE.verify(self.root)


if __name__ == "__main__":
    unittest.main()
