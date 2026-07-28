Discord playtest webhook
========================

Runtime lookup:
1) XOR-embedded bytes in DiscordWebhookEmbedded.Data.cs (player / CI builds)
2) Resources/Debug/DiscordWebhookSettings (Editor Play Mode only; gitignored)

NOT stored as plaintext StreamingAssets.

CI (push → Windows build):
1. GitHub secret DISCORD_PLAYTEST_WEBHOOK_URL
2. Workflow runs BuildSupport/Stamp-DiscordWebhookEmbedded.ps1
3. Unity compiles obfuscated bytes into the player — no .url file beside the exe

Editor Play Mode:
Game → Debug → Open Discord Webhook Settings → paste URL

Local player build without CI:
Game → Debug → Stamp Discord Webhook Embed From Settings
then build

Note: a determined reverse-engineer can still recover client secrets.
This only stops casual browsing of a plaintext webhook file.
