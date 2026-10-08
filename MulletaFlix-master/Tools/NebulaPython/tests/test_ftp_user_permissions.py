from __future__ import annotations

import asyncio
import importlib
import unittest
from pathlib import PurePosixPath

server = importlib.import_module("ftp.server")


class LegacyUserCollection:
    async def find_one(self, _query):
        return {
            "login": "nebula-user",
            "password_hash": "hash",
            "permissions": "elradfmwM",
        }


class LegacyUserDatabase:
    users = LegacyUserCollection()


class TestUserPermissionSchema(unittest.TestCase):
    def test_legacy_permission_flag_string_does_not_broaden_access(self):
        user = server.User.from_dict(
            {
                "login": "nebula-user",
                "password_hash": "$2b$fixture",
                "permissions": "elradfmwM",
            }
        )

        self.assertEqual(user.home_path, PurePosixPath("/nebula-user"))
        self.assertTrue(user.get_permissions("/nebula-user/file.mkv").writable)
        self.assertFalse(user.get_permissions("/OtherLibrary/file.mkv").readable)
        self.assertFalse(user.get_permissions("/OtherLibrary/file.mkv").writable)

    def test_ftp_user_lookup_survives_legacy_mongo_document(self):
        manager = server.MongoDBUserManager(LegacyUserDatabase())

        state, user, message = asyncio.run(manager.get_user("nebula-user"))

        self.assertEqual(state, server.AbstractUserManager.GetUserResponse.PASSWORD_REQUIRED)
        self.assertEqual(user.login, "nebula-user")
        self.assertEqual(message, "password required")

    def test_valid_path_acl_is_preserved_without_mutating_source_document(self):
        acl = {"path": "/Shared", "readable": True, "writable": False}
        document = {"login": "nebula-user", "password_hash": "hash", "permissions": [acl]}

        user = server.User.from_dict(document)

        self.assertTrue(user.get_permissions("/Shared/title.mkv").readable)
        self.assertFalse(user.get_permissions("/Shared/title.mkv").writable)
        self.assertEqual(acl, {"path": "/Shared", "readable": True, "writable": False})

    def test_malformed_permissions_fail_closed(self):
        user = server.User.from_dict(
            {
                "login": "nebula-user",
                "password_hash": "hash",
                "permissions": ["elradfmwM", {"path": "../../outside", "writable": True}],
            }
        )

        self.assertFalse(user.get_permissions("/OtherLibrary/file.mkv").readable)
        self.assertFalse(user.get_permissions("/OtherLibrary/file.mkv").writable)


if __name__ == "__main__":
    unittest.main()
