"""Minimal Python HTTP playback worker supervised by the MulletaFlix host."""
from __future__ import annotations

import asyncio
import contextlib
import logging
import os
from pathlib import Path

import main as nebula
from motor.motor_asyncio import AsyncIOMotorClient
from pyrogram import Client
from pyrogram.errors import FloodWait


async def run() -> int:
    try:
        api_id, api_hash, tokens = nebula.get_required_config()
    except RuntimeError as exc:
        nebula.logger.critical("%s", exc)
        return 1

    sessions_dir = Path(os.environ.get("SESSIONS_DIR", Path.home() / ".nebulaftp" / "stream-sessions"))
    sessions_dir.mkdir(parents=True, exist_ok=True)
    bots = [
        Client(
            f"Nebula_Stream_Bot_{index + 1}",
            api_id=api_id,
            api_hash=api_hash,
            bot_token=token,
            no_updates=True,
            workers=1,
            max_concurrent_transmissions=1,
            workdir=str(sessions_dir),
        )
        for index, token in enumerate(tokens)
    ]
    for bot, token in zip(bots, tokens, strict=True):
        bot._nebula_bot_token = token

    async def start_bot(bot):
        try:
            await asyncio.wait_for(bot.start(), timeout=15)
            return bot
        except (asyncio.TimeoutError, FloodWait, Exception) as exc:
            nebula.logger.warning("Bot de playback indisponível (%s)", exc)
            with contextlib.suppress(Exception):
                await bot.stop()
            return None

    active_bots = [bot for bot in await asyncio.gather(*(start_bot(bot) for bot in bots)) if bot]
    if not active_bots:
        nebula.logger.error("Nenhum bot Telegram autenticado para playback")
        return 1

    mongo_client = None
    server = None
    try:
        mongo_client = AsyncIOMotorClient(
            os.environ["MONGODB"],
            serverSelectionTimeoutMS=int(os.environ.get("MONGO_SERVER_SELECTION_TIMEOUT_MS", "5000")),
            connectTimeoutMS=int(os.environ.get("MONGO_CONNECT_TIMEOUT_MS", "5000")),
        )
        database = mongo_client[os.environ.get("MONGO_DATABASE", "ftp")]
        await database.command("ping")
        nebula.MongoDBPathIO.db = database
        nebula.MongoDBPathIO.tg = active_bots
        server = await nebula.start_http_stream_server(database, active_bots)
        nebula.logger.info("Worker Python de playback pronto em %s:%s", nebula.STREAM_HOST, nebula.STREAM_PORT)
        await server.serve_forever()
        return 0
    finally:
        if server:
            server.close()
            await server.wait_closed()
        if mongo_client:
            mongo_client.close()
        await asyncio.gather(*(bot.stop() for bot in active_bots), return_exceptions=True)


if __name__ == "__main__":
    try:
        raise SystemExit(asyncio.run(run()))
    except KeyboardInterrupt:
        raise SystemExit(0)
