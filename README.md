# QuickOTP

Utilitaire Windows qui centralise les codes OTP / 2FA depuis Gmail et Outlook, les detecte localement et les copie dans le presse-papiers.

**100 % local** — aucun contenu d'e-mail ni OTP n'est envoye vers un serveur tiers.

## Etat du depot

Ce depot contient actuellement :

- `src/QuickOTP.Launcher` — lanceur WinExe avec **auto-update** (GitHub Releases)
- `scripts/` — publish / install
- Les binaires de l'app WPF (`QuickOTP` / `QuickOTP.Core`) sont distribues dans les [Releases](https://github.com/bipowww/QuickOTP/releases)

> `QuickOTP.Core` a ete restaure par decompilation ILSpy et **compile a nouveau**.
> L'UI WPF (`src/QuickOTP`) est presente en forme decompilee (BAML) mais **pas encore rebuildable** :
> le package shippe encore `QuickOTP.dll` / `QuickOTPApp.exe` depuis `artifacts/app`.

## Installation (amis)

1. Telecharger `QuickOTP-*-win-x64.zip` depuis [Releases](https://github.com/bipowww/QuickOTP/releases/latest)
2. Dezipper et lancer `QuickOTP.exe` (ou `scripts\Install-Local.ps1` apres build)
3. Prerequis : [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
4. Placer votre propre `client_secret.json` dans `%LocalAppData%\QuickOTP\`

Au demarrage, le lanceur verifie `update.json` sur GitHub et installe la nouvelle version si le SHA256 correspond.

## Layout install

| Role | Chemin |
|------|--------|
| Lanceur + binaires app | `%LocalAppData%\Programs\QuickOTP\` |
| Donnees user (tokens DPAPI, accounts, secret) | `%LocalAppData%\QuickOTP\` |

Ne jamais melanger secrets et package de release.

## Publier une maj

```powershell
# Build lanceur + zip + update.json (UTF-8 sans BOM, sans PDB)
.\scripts\Publish-Update.ps1 -Version "1.0.1"

# Publier sur GitHub
.\scripts\Publish-Update.ps1 -Version "1.0.1" -PublishGitHub
```

Sources app acceptees (dans l'ordre) :

1. `-AppSource <dossier>`
2. `publish\app` / `artifacts\app`
3. `%LocalAppData%\Programs\QuickOTP\app`

## Configuration Gmail OAuth

1. [Google Cloud Console](https://console.cloud.google.com/) → projet
2. Activer **Gmail API**
3. OAuth Desktop + scopes `gmail.readonly`, `userinfo.email`
4. Copier le JSON vers `%LocalAppData%\QuickOTP\client_secret.json`

Voir `client_secret.example.json`. **Ne jamais committer** le vrai fichier.

## Configuration Microsoft OAuth

Voir `msal_client.example.json`. Emplacement : `%LocalAppData%\QuickOTP\msal_client.json`.

## Securite

- Tokens OAuth et historique : DPAPI CurrentUser
- Pas de telemetrie OTP
- Auto-update : HTTPS + SHA256 obligatoire
- PDB et secrets exclus des packages

## Limitations actuelles

- Sources app absentes du repo → pas de rebuild depuis CI
- Pas de signature Authenticode encore
- Polling Gmail intelligent (pas de Pub/Sub distant)
