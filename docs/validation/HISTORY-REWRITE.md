# History rewrite, 2026-11-09

At the owner's request, personal details were removed from this repository's history and from the published source archives. This file records what changed so older notes still make sense.

## What was removed

- A Windows user name inside absolute workspace paths in `V0.6.3-release-evidence.json` and `V0.6.4-release-evidence.json` (now `<workspace>`).
- A real city with coordinates in a form-shot fixture in `src/Tests/Program.cs` (now the sample city the previews use).
- A personal e-mail address as the author and committer of every commit (now the account's GitHub no-reply address).

Nothing else differs: in every rewritten commit only those three files changed, dates and messages are the same, and the final tree is identical to the one before the rewrite.

## Commits

Every commit got a new id. `main` and the six release tags were force-pushed. Hashes quoted in older documents, and the `+<hash>` in each executable's product version, are the **old** ids.

| Old | New | Commit |
|---|---|---|
| `e7d4f86` | `85ab972` | Pulsewave 0.9.1 plus unreleased 0.9.2 fixes |
| `bc78b32` | `78cafae` | Configure Matt Pocock's engineering skills |
| `fb67fa1` | `e80ece8` | Record the agreed plan after 0.9.2 in the roadmap |
| `8e4170d` | `807fd59` | Release 0.9.2 |
| `7ca5fb5` | `4b87502` | Record the 0.9.2 release evidence |
| `d365371` | `9e6cc72` | Release 0.10.0: the HUD picks the theme for the moment |
| `5078f80` | `c02561f` | Record the 0.10.0 release evidence |
| `bf109ed` | `5a632d5` | Release 0.11.0: frame time, RTSS 1% low and shortcut lists |
| `3911fb3` | `9543d30` | Record the 0.11.0 release evidence |
| `394330e` | `5b68160` | Release 0.12.0: hotkeys for any action |
| `5c1cfca` | `45b4ca8` | Record the 0.12.0 release evidence |
| `203320a` | `0028445` | Add the Lumen theme family and restyle the HUD pages to match |
| `e21fbfe` | `428022a` | Make Lumen the design baseline |
| `1d62852` | `aec7cf3` | Release 0.13.0: Lumen, the new baseline look |
| `0bfc32f` | `80defe2` | Record the 0.13.0 release evidence |
| `ab0ae5d` | `18625ed` | Rewrite the README as the public project page; remove personal details |

## Source archives

The source archive of each release from 0.9.1 to 0.13.0 was rebuilt with that one fixture line changed (every other entry byte-identical), uploaded in place of the original, and the SHA-256 in its release notes and evidence file updated. The Windows packages and executables were not changed: they never contained these details.

| Release | Old SHA-256 | New SHA-256 |
|---|---|---|
| 0.9.1 | `07F5C6A73F23751AD06D82E059FC6E07AC7F2377D04940BEF9307A0BC587D6D0` | `A83C11B7B314392EAB004DE371EA0D9E24FFFEB22C665E61C2F5F3E93A4D831A` |
| 0.9.2 | `0BD6F6ACD97EE84015EE5B52CC392B751E1E523B7DF9B52F9D8765D1B364D806` | `9AD488CC1252EF19CF3FFA07F6029FD302834A886BAE12174D15B2F6BF6D1CB6` |
| 0.10.0 | `F1D99FF110DB09EC4CD01E9F7C7FB8A6943686DC6371EF995CADEFA658848139` | `210CAC053E9180EC05070AB9C43C15C446E305F82B8FDD0CD27508144F1D3F30` |
| 0.11.0 | `97424C776A5C4CBCBA89FDAAD230EEA1ACCBA2FA9123A22F0700A44D7683671F` | `1C8D3EDDC04F3CFFE21C43CB02E63C7D92895B6E7399DBBA2E6438FB0659FCCD` |
| 0.12.0 | `08362CDA4EB52B631FCC937A0F29F8D0497CBD7EDCAF9085CCC04223D759F619` | `BC94EAC4034F7E54C00D0296A93939E5C9DCAA0B02AE3993834DD000C3B57B1D` |
| 0.13.0 | `7DFC147FDB3177AAD4F9F6618E7C47F2E3BE085AB8F8849FDC689D66EA52D844` | `C57D064D6679B8A0D4062BDADFB2088D01D7152C5A59F95C1C83E9C12D761D0E` |

## Limits

- GitHub can keep unreachable commits for a while and serve them to anyone who has the old id; only GitHub Support can purge them. The repository was private with no forks at the time.
- Copies made before the rewrite (clones, downloaded archives) still hold the old content.
- A local backup of the old refs and archives, kept outside git during the rewrite, was deleted the same day at the owner's request once the result was verified. The owner's local git store may still hold the old commits as unreachable objects until git prunes them.
