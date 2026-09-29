# Email Archive Indexer

[English](README.md) · [한국어](README.ko.md) · [Español](README.es.md) · **Français** · [日本語](README.ja.md) · [简体中文](README.zh.md)

Un programme Windows portable pour ceux qui conservent leur courrier sous forme de fichiers `.msg` / `.eml`. Il retrouve en quelques secondes n'importe quel courrier dans un dossier de sauvegarde contenant jusqu'à environ 10 000 fichiers, donne aux fichiers des noms cohérents, nettoie les doublons et sauvegarde automatiquement votre Boîte de réception et vos Éléments envoyés d'Outlook (classique).

Un seul fichier `.exe`, sans installation, sans droits d'administrateur, sans connexion Internet.

![Fenêtre principale](docs/images/main-en.png)

*Capture d'écran réalisée sous Mono sur Linux avec des données d'exemple ; l'apparence réelle sous Windows est légèrement différente.*

## Fonctionnalités

- **Recherche rapide** dans l'objet, les personnes (De/À/Cc), le corps et les noms des pièces jointes. Plusieurs mots = tous doivent correspondre ; `"guillemets"` = expression exacte.
- **Filtres** : date, reçu/envoyé, pièces jointes, réponses aux réunions (invitation/acceptée/refusée/provisoire/annulée), doublons, courrier similaire, erreurs de lecture, dossier, principaux expéditeurs.
- **Aperçu** en affichage mis en forme ou en affichage texte. Les images externes, les scripts et les redirections sont bloqués, et une confirmation est demandée avant d'ouvrir un lien. Ouvrez le courrier dans Outlook avec Ctrl+O.
- **Normalisation des noms de fichiers** au format `YYMMDD_HHMMSS_Expéditeur_Objet_AttY.msg`, en utilisant l'heure de réception pour le courrier reçu et l'heure d'envoi pour le courrier envoyé. Un aperçu avant/après s'affiche d'abord, et vous pouvez annuler le renommage.
- **Doublons** : les copies exactes ajoutées au dossier sont automatiquement envoyées à la Corbeille (jamais lors de la première analyse). Un nettoyage guidé traite le reste. Rien n'est jamais supprimé définitivement.
- **Sauvegarde Outlook (classique)** : enregistre votre Boîte de réception et vos Éléments envoyés au format `.msg`, ignore le courrier déjà sauvegardé, ne modifie rien dans Outlook et consigne chaque réussite et chaque échec. Elle démarre Outlook s'il n'est pas ouvert.
- **Analyses incrémentielles** grâce à un petit cache local : après la première analyse, seuls les fichiers modifiés sont lus.
- **6 langues** : English, 한국어, Español, Français, 日本語, 简体中文 (Paramètres → Language / 언어).

## Téléchargement et exécution

1. Téléchargez `EmailIndexer-v<version>.zip` depuis [Releases](https://github.com/mixqz/msg_indexer/releases) et décompressez-le.
2. Double-cliquez sur `EmailIndexer.exe`. L'application n'étant pas signée numériquement, Windows peut afficher **« Windows a protégé votre ordinateur »**. Choisissez **Informations complémentaires → Exécuter quand même**, ou cliquez avec le bouton droit sur le fichier → **Propriétés** → **Débloquer**.
3. Cliquez sur **Changer de dossier** et choisissez votre dossier de sauvegarde du courrier.

Configuration requise : Windows 11 (testé) ou Windows 10 avec .NET Framework 4.8, inclus dans Windows. La sauvegarde Outlook nécessite Outlook (classique) ; le nouvel Outlook ne dispose d'aucune interface d'automatisation.

Guide de l'utilisateur : [English](docs/guide/USER_GUIDE.en.md) · [한국어](docs/guide/USER_GUIDE.ko.md) · [Español](docs/guide/USER_GUIDE.es.md) · [Français](docs/guide/USER_GUIDE.fr.md) · [日本語](docs/guide/USER_GUIDE.ja.md) · [简体中文](docs/guide/USER_GUIDE.zh.md)

## Confidentialité

Tout reste sur votre PC. L'application n'établit aucune connexion réseau. Son cache et ses journaux se trouvent dans un dossier masqué `.emailindex` à l'intérieur de votre dossier de sauvegarde et dans `%APPDATA%\EmailIndexer`.

## Compilation à partir des sources

Seul Docker est nécessaire ; rien n'est installé sur la machine hôte.

```bash
./build.sh          # tests + Windows exe → dist/EmailIndexer.exe
./build.sh test     # core tests only
python3 package.py  # release zip → dist/EmailIndexer-v<version>.zip (+ .sha256)
```

- `src/EmailIndexer.Core` : analyse du courrier, cache, doublons, règles de nom de fichier, traductions (netstandard2.0, testé dans Docker)
- `src/EmailIndexer.App` : interface WinForms et COM Outlook (.NET Framework 4.8, fusionnés en un seul exe)
- `tests/EmailIndexer.Core.Tests` : tests xUnit
- Version : incrémentez ensemble `src/EmailIndexer.Core/AppInfo.cs`, `src/EmailIndexer.App/EmailIndexer.App.csproj` et `src/EmailIndexer.App/app.manifest`.
- Diagnostic : `EmailIndexer.exe --scan-test <folder>` (rapport d'analyse en lecture seule) et `--index-test <folder>` (rapport sur le cache et les doublons, ne supprime rien).

## Contribuer

Les corrections de traduction et les nouvelles langues sont les bienvenues ; les traductions ont été rédigées avec l'aide de l'IA et n'ont pas encore été relues par des locuteurs natifs. Consultez [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

[MIT](LICENSE). Les composants tiers sont répertoriés dans [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
