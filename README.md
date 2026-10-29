# QuickOTP

Utilitaire Windows natif et léger qui centralise les codes de vérification (OTP / 2FA / MFA) depuis Gmail et Outlook / Microsoft 365, les détecte localement et les copie dans le presse-papiers.

**100 % local** — aucun contenu d’e-mail ni OTP n’est envoyé vers un serveur tiers.

## Fonctionnalités

- Multi-comptes **Gmail** (OAuth, `gmail.readonly`)
- Comptes **Outlook / Hotmail / Microsoft 365** (Microsoft Graph, `Mail.Read`)
- Moteur universel `OtpDetectionEngine` (indépendant des sources)
- Détection de service (Discord, Google, Amazon, GitHub, …)
- Smart Clipboard (effacement conditionnel)
- Expiration intelligente + compte à rebours
- Historique local chiffré DPAPI
- Raccourci global `Ctrl+Alt+V`
- Fenêtre **Quick Codes** (mini)
- Modes notification Normal / Private / Silent
- Privacy Mode (masque les codes à l’écran)
- Windows Hello pour l’historique (optionnel)
- Règles locales (allow / deny / overrides)
- Signalement « Not a verification code »
- Tray, instance unique, démarrage silencieux Windows
- Architecture abonnement (provider de développement)

## Surveillance Gmail (polling adaptatif)

QuickOTP utilise principalement `users.history.list` + `historyId` (jamais `messages.list` en boucle de polling).
`messages.list` n’est utilisé qu’en **recovery exceptionnelle** (HTTP 404 historyId expiré), fenêtre `newer_than:5m`, max 10 messages.

Priorité des modes : Backoff → WakeBoost → NetworkRecovery → StartupBoost → RecentActivity → Normal → Idle → DeepIdle
(BudgetCooldown local au-dessus de Backoff).

| État | Intervalle |
|------|----------:|
| Startup &lt;2 min | **2 s** |
| Wake &lt;2 min | **2 s** (+ poll immédiat) |
| Network recovery &lt;2 min | **2 s** (+ poll immédiat) |
| Recent activity (2 min) | **3 s** |
| Normal | **5 s** |
| Idle &gt;5 min | **15 s** |
| Idle &gt;30 min (Deep Idle) | **30 s** |
| Backoff (429/5xx/réseau) | 5→10→20→40→**60 s** |
| Budget API local dépassé | cooldown **30 s** |

Jitter ±10 % injecté (désactivable / seedé pour les tests). Stagger initial entre comptes.

Filtrage `history.list` : `historyTypes=messageAdded` + `labelId=INBOX` + pagination `nextPageToken` (plafond 10 pages).
`messages.get` uniquement pour les IDs nouveaux dédupliqués.

Budget local / compte : history.list ≤120/min, messages.get ≤60/min, total ≤180/min.

- Une seule boucle coordonnée + `Task.Delay` interruptible (réveil Windows / réseau).
- Stats locales Avg/P50/P90/P95/P99 (onglet Diagnostics) — jamais envoyées.
- Au repos profond (~30 s) : **≈ 120 appels `history.list` / heure / compte**.
- Latence affichée = détection locale − `ReceivedAt` Gmail (approximation, pas la latence Gmail pure).

## Prérequis

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) pour compiler

## Configuration Gmail OAuth

1. [Google Cloud Console](https://console.cloud.google.com/) → créer un projet
2. Activer **Gmail API**
3. Écran de consentement OAuth (External) + utilisateur de test
4. Credentials → **OAuth client ID** → type **Desktop**
5. Scopes : `gmail.readonly`, `userinfo.email`
6. Copier le JSON téléchargé vers `client_secret.json` (racine, `%LocalAppData%\QuickOTP\`, ou à côté de l’exe)

Voir `client_secret.example.json`. **Ne jamais committer** le vrai fichier.

## Configuration Microsoft OAuth

1. [Azure Portal](https://portal.azure.com/) → **App registrations** → New
2. Type de compte : comptes personnels + professionnels (`common`)
3. Authentication → plateforme **Mobile and desktop** → redirect `http://127.0.0.1`
4. API permissions : `User.Read`, `Mail.Read` (delegated) → consent
5. Créer un client secret **uniquement si** vous utilisez un client confidentiel ; pour un client public desktop, le secret peut être omis
6. Placer `msal_client.json` :

```json
{
  "client_id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "client_secret": ""
}
```

Voir `msal_client.example.json`. Emplacements : à côté de l’exe ou `%LocalAppData%\QuickOTP\`.

## Stockage sécurisé

| Donnée | Emplacement | Protection |
|--------|-------------|------------|
| Tokens OAuth | `%LocalAppData%\QuickOTP\tokens\*.dpapi` | DPAPI CurrentUser |
| Historique OTP | `%LocalAppData%\QuickOTP\history.dpapi` | DPAPI |
| Préférences | `settings.json` | Pas de secrets |
| Comptes (email, curseurs) | `accounts.json` | Pas de tokens |
| Règles | `rules.json` | Local |

## Build / test / run

```powershell
dotnet restore QuickOTP.sln
dotnet build QuickOTP.sln -c Release
dotnet test QuickOTP.sln -c Release
dotnet run --project src\QuickOTP\QuickOTP.csproj -c Release
```

Publication autonome :

```powershell
dotnet publish src\QuickOTP\QuickOTP.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

Puis copier `client_secret.json` / `msal_client.json` dans `.\publish\`.

Installateur : `installer\QuickOTP.iss` (Inno Setup) après publish.

## Paramètres notables

- Launch at Windows start (HKCU Run, `--autostart` → aucune fenêtre)
- Auto-copy, notifications (Normal / Private / Silent)
- Clear clipboard after 30s–10m (seulement si le contenu n’a pas changé)
- History retention : Disabled / 15m / 1h / 24h
- Hotkey configurable
- Privacy Mode
- Windows Hello for history
- Log level : Off / Errors only / Diagnostic (jamais d’OTP/tokens dans les logs)

## Sécurité

- Pas de mot de passe mail stocké
- Pas de télémétrie OTP
- Pas d’envoi de corps d’e-mails
- Logs sanitizés
- L’abonnement ne bloque jamais la détection (cache + grace)

## Roadmap

- Bridge Android SMS → `IAndroidSmsOtpSource`
- Authenticator TOTP local (`ITotpOtpSource`, secrets chiffrés)
- Provider Stripe réel derrière `ISubscriptionService`

## Limitations

- Polling intelligent (pas de Pub/Sub distant) — intervalle 5–60 s
- Microsoft Graph : filtre temporel inbox (pas de deltaLink complet dans cette version)
- Windows Hello se dégrade gracieusement si indisponible

## Distribution & mises à jour automatiques

Les amis installent le **paquet lanceur** (pas seulement l’exe WPF). Au démarrage, `QuickOTP.exe` consulte `update.json` sur GitHub Releases et propose d’installer la nouvelle version.

### Pour tes amis (première install)

1. Télécharger `QuickOTP-*-win-x64.zip` depuis [Releases](https://github.com/bipowww/QuickOTP/releases/latest)
2. Dézipper (ou `scripts\Install-Local.ps1` après build)
3. Prérequis : [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
4. Placer leur propre `client_secret.json` dans `%LocalAppData%\QuickOTP\`

### Pour publier une maj (toi)

```powershell
# 1) Build + zip + update.json
.\scripts\Publish-Update.ps1 -Version "1.0.1"

# 2) Publier sur GitHub (gh auth login une fois)
.\scripts\Publish-Update.ps1 -Version "1.0.1" -PublishGitHub
```

Cela crée la release `v1.0.1` avec le zip et `update.json`. Les installs existantes voient la maj au prochain lancement.
