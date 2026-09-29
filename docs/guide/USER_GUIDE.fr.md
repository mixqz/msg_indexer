# Email Archive Indexer — Guide de l'utilisateur

Un programme Windows portable (sans installation) qui vous permet de retrouver et d'organiser rapidement vos fichiers de sauvegarde du courrier Outlook (.msg / .eml), et de sauvegarder automatiquement le courrier depuis Outlook.

## 1. Prise en main

1. Placez le fichier unique `EmailIndexer.exe` où vous le souhaitez (par exemple, sur le Bureau ou dans Documents). Aucune installation n'est nécessaire.
2. Double-cliquez dessus pour l'exécuter.
   - Si **« Windows a protégé votre ordinateur »** s'affiche → **Informations complémentaires** → **Exécuter quand même**
   - S'il ne s'exécute toujours pas : cliquez avec le bouton droit sur le fichier → **Propriétés** → cochez **Débloquer** en bas → OK → exécutez-le de nouveau
3. Cliquez sur **Changer de dossier** et choisissez votre dossier de sauvegarde du courrier. (Vous pouvez aussi faire glisser un dossier de l'Explorateur de fichiers vers la fenêtre.)
4. La première fois, tout le dossier est lu. Ensuite, seuls les fichiers modifiés sont relus : la liste s'ouvre en quelques secondes.

> Ne l'exécutez pas en tant qu'administrateur. S'il s'exécute à un niveau d'autorisation différent de celui d'Outlook, la sauvegarde Outlook ne fonctionnera pas.

**Langue :** l'application démarre en anglais. Pour changer de langue, ouvrez **Paramètres** → **Language / 언어**, choisissez une langue, puis redémarrez l'application. Chaque langue est affichée dans sa propre langue (English, 한국어, Español, Français, 日本語, 简体中文).

## 2. La fenêtre principale

| Zone | Rôle |
|---|---|
| Haut | Dossier de sauvegarde, **Analyser (F5)**, **Sauvegarde Outlook**, **Paramètres**, **Aide (F1)** |
| Filtres (à gauche) | Date · Reçu / Envoyé · Pièces jointes · Événement (invitation/acceptée/refusée/provisoire/annulée) · État (doublon/similaire/erreur/non normalisé) · Format · Dossier · 30 principaux expéditeurs |
| Zone de recherche | Recherche à la fois dans l'objet, les personnes, le corps et les noms des pièces jointes. Plusieurs mots = tous doivent correspondre, `"guillemets"` = expression exacte. Utilisez les boutons voisins pour limiter la portée |
| Liste | Cliquez sur un en-tête de colonne pour trier. Gris = doublon, orange = similaire, rouge = erreur de lecture |
| Bas | Nombre total · affichés · sélectionnés, résultat de l'analyse |

## 3. Tâches courantes

| Pour | Procédez ainsi |
|---|---|
| Lire rapidement un courrier | Double-cliquez ou appuyez sur Enter → basculez entre **Affichage mis en forme** et **Affichage texte**, ◀ ▶ pour le précédent/suivant |
| Ouvrir dans Outlook | Ctrl+O ou **Ouvrir (Ctrl+O)** |
| Sélectionner plusieurs courriers | Ctrl/Shift + clic, Ctrl+A |
| Aller à la zone de recherche | Ctrl+F (Esc efface la recherche) |
| Menu contextuel (clic droit) | Aperçu · Ouvrir (Outlook) · Afficher dans le dossier · Copier le chemin · Normaliser les noms de fichiers · Déplacer la sélection… · Déplacer vers la Corbeille · Afficher uniquement cet expéditeur |
| Savoir comment fonctionne une fonctionnalité | F1 ou **Aide (F1)** |

## 4. Organiser les fichiers

- **Normaliser les noms** : renomme les fichiers au format `260823_175434_Alex Kim [Équipe commerciale]_Ordre du jour équipe S35_AttN.msg`.
  - Le courrier reçu utilise l'heure de réception ; le courrier envoyé, l'heure d'envoi.
  - Le marqueur de pièce jointe à la fin suit la langue de l'application (en français `_AttY`/`_AttN`). Les fichiers déjà normalisés, quelle que soit la langue, sont laissés tels quels.
  - Si rien n'est sélectionné, tout le courrier affiché dans la liste est inclus.
  - Avant toute modification, un tableau « avant → après » s'affiche. **Annuler le dernier renommage**, dans la même fenêtre, rétablit les noms d'origine.
  - Les caractères comme `: / ?` dans l'objet deviennent des caractères pleine chasse d'aspect similaire (`： ／ ？`).
- **Déplacer la sélection** : déplace les fichiers vers le dossier de votre choix. Si un fichier du même nom existe, ` (2)` est ajouté.
- **Supprimer la sélection (Del)** : envoie les fichiers dans la **Corbeille**, d'où vous pouvez les restaurer.
- **Nettoyer les doublons** : affiche dans un tableau les fichiers confirmés comme étant le même courrier, puis les envoie dans la Corbeille. Un original est conservé par groupe.
- **Suppression automatique des doublons** : si vous copiez un courrier déjà présent, la copie ajoutée part automatiquement dans la Corbeille lors de l'analyse suivante.
  - Lors de la toute première analyse, rien n'est supprimé automatiquement ; les doublons sont seulement marqués.
  - Les fichiers que vous restaurez depuis la Corbeille ne sont pas supprimés de nouveau.
  - Les fichiers simplement renommés ou déplacés vers un autre dossier ne sont pas considérés comme des doublons.

## 5. Sauvegarde automatique d'Outlook

1. Si Outlook (classique) n'est pas ouvert, il démarre automatiquement. Si un sélecteur de profil s'affiche, choisissez votre profil. (Le nouvel Outlook n'est pas pris en charge.)
2. **Sauvegarde Outlook** → choisissez une plage (Depuis la dernière sauvegarde / Derniers N jours / Tout) → **Démarrer la sauvegarde**
3. Le courrier de la Boîte de réception et des Éléments envoyés est enregistré **directement dans le dossier de sauvegarde** (sans sous-dossiers) avec des noms normalisés. Vous pouvez ensuite déplacer vous-même les fichiers dans des sous-dossiers ; l'analyse les retrouve tous.
4. Le courrier déjà sauvegardé est ignoré. Le courrier d'origine dans Outlook n'est pas modifié.
5. Si Outlook affiche « Un programme tente d'accéder… », cliquez sur **Autoriser**.

## 6. Résolution des problèmes

| Problème | Solution |
|---|---|
| Sauvegarde Outlook : « connexion impossible » | Redémarrez Outlook et ce programme normalement (double-clic, pas en tant qu'administrateur) |
| Sauvegarde Outlook : message « nouvel Outlook » | Désactivez le commutateur **Nouvel Outlook** en haut à droite d'Outlook pour revenir à la version classique |
| Fichiers « Erreur » en rouge dans la liste | Le fichier est endommagé ou n'est pas un fichier de courrier. Le motif s'affiche dans la colonne Objet |
| Un comportement semble anormal | Envoyez le résultat de **Paramètres** → **Vérifier l'environnement** ainsi que les fichiers journaux ci-dessous à votre contact d'assistance |

**Fichiers journaux** (joignez-les lorsque vous signalez un problème ; ils sont toujours rédigés en anglais)
- `dossier de sauvegarde\.emailindex\last-scan.log` — résultat de la dernière analyse et motifs des erreurs
- `dossier de sauvegarde\.emailindex\actions.log` — historique des renommages, déplacements et suppressions
- `dossier de sauvegarde\.emailindex\outlook-backup.log` — historique des sauvegardes Outlook
- `%APPDATA%\EmailIndexer\error.log` — erreurs inattendues

Le dossier masqué `.emailindex` est un cache qui permet d'ouvrir la liste rapidement. Le supprimer n'affecte pas votre courrier ; il est entièrement reconstruit à la prochaine exécution. (Après cela, la suppression automatique des doublons est suspendue pendant une analyse, comme lors d'une première analyse, et le point de référence « Depuis la dernière sauvegarde » de la sauvegarde Outlook est perdu.)
