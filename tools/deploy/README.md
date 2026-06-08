# Deploy tools

Mac → Steam Deck iteration helpers for the prototype: build on the Mac, `rsync` the Linux x86_64
build to the Deck, and stream its logs back over SSH.

| Script | Purpose |
|---|---|
| `deploy.sh` | `rsync` the Linux x86_64 build to the Deck (Method B — non-Steam game). |
| `logtail.sh` | Stream the Deck's Unity `Player.log` over SSH for debugging. |

## Configure SSH (one-time)

This lets the Mac push builds (`deploy.sh`) and stream logs (`logtail.sh`) to the Deck.

**On the Steam Deck** — Steam button → Power → *Switch to Desktop*, then open **Konsole**:

1. Set a password (the Deck has none by default; SSH needs one):
   ```bash
   passwd
   ```
2. Enable and start the SSH server:
   ```bash
   sudo systemctl enable --now sshd
   ```
3. Note the Deck's address: hostname `steamdeck.local`, or find its IP with `ip addr`.
   > After a major SteamOS update you may need to re-run step 2.

**On the Mac** — Terminal:

1. Create an SSH key if you don't already have one (press Enter for the defaults):
   ```bash
   ssh-keygen -t ed25519
   ```
2. Copy your public key to the Deck (enter the password from above when prompted):
   ```bash
   ssh-copy-id deck@steamdeck.local
   ```
3. Verify a password-less login works:
   ```bash
   ssh deck@steamdeck.local
   ```

Optional — add this to `~/.ssh/config` so you can use `DECK_HOST=deck` with the scripts:

```
Host deck
    HostName steamdeck.local
    User deck
```

## Usage

```bash
# Build in Unity to unity/Build/Linux/ first, then:
DECK_HOST=steamdeck.local GAME_NAME=PhysicsProto bash tools/deploy/deploy.sh

# Watch logs while it runs on the Deck:
COMPANY=DefaultCompany PRODUCT=PhysicsProto DECK_HOST=steamdeck.local bash tools/deploy/logtail.sh
```

All defaults are overridable via environment variables (see the top of each script). `GAME_NAME`,
`COMPANY`, and `PRODUCT` must match Unity's Player Settings once the project is created.
