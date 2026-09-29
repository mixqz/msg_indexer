# Translation glossary

Source language: English (`src/EmailIndexer.Core/Lang/en*.lang`). Terms follow the official Microsoft Windows / Outlook terminology of each language, so users see the same words they know from Outlook and File Explorer.

## Style

| | es | fr | ja | zh (Simplified) | ko |
|---|---|---|---|---|---|
| Address the user | tú (Microsoft style) | vous | です・ます調 (buttons: noun/verb stem) | 你 (Microsoft style) | 합니다체 (buttons: 명사형) |
| Buttons / menu items | Infinitive: "Mover", "Eliminar" | Infinitive: "Déplacer", "Supprimer" | 「移動」「削除」 | 「移动」「删除」 | 「이동」「삭제」 |
| Sentence case | Yes | Yes | – | – | – |
| Punctuation | ¿…? ¡…! | Space before `: ; ? !` (use normal space) | 「」 and 、。 | ，。：（ ） full-width in sentences; half-width parentheses around English/code | – |

## Fixed rules

- Keep every key unchanged. Translate only the text after `=`.
- Keep placeholders byte-for-byte: `{0}`, `{1:#,0}`, `{2}`… (order may change, the set may not).
- Keep escapes `\n`, `\t` and line structure. One `key = value` per line.
- Plural keys (`x.one` / `x.other`): es and fr use both forms (fr: 0 and 1 are `.one`). ja, zh, ko have no plural: provide both keys with the same text.
- Do not translate: `Email Archive Indexer`, `Outlook`, `Windows`, `msg`, `eml`, `Message-ID`, `EntryID`, file names/paths, `%APPDATA%`, `.emailindex`, key names `Ctrl`, `Shift`, `Alt`, `F1`, `F5`, `Enter`, `Esc`, `Del`, `Tab`.
- File-name markers shown in examples use that language's marker: en/es/fr `_AttY`/`_AttN`, ko `_첨부O`/`_첨부X`, ja `_添付有`/`_添付無`, zh `_有附件`/`_无附件`.
- Button labels stay short (es/fr grow about 30%; prefer the shortest correct Microsoft term).

## Terms

| English | es | fr | ja | zh | ko |
|---|---|---|---|---|---|
| mail / email | correo | courrier | メール | 邮件 | 메일 |
| attachment | datos adjuntos (count: archivo adjunto) | pièce jointe | 添付ファイル | 附件 | 첨부 |
| inline image (signature logo) | imagen insertada | image incorporée | インライン画像 | 内嵌图片 | 본문 이미지 |
| Inbox | Bandeja de entrada | Boîte de réception | 受信トレイ | 收件箱 | 받은편지함 |
| Sent Items | Elementos enviados | Éléments envoyés | 送信済みアイテム | 已发送邮件 | 보낸편지함 |
| Received / Sent (direction) | Recibido / Enviado | Reçu / Envoyé | 受信 / 送信 | 收到 / 发出 | 받음 / 보냄 |
| sender | remitente | expéditeur | 差出人 | 发件人 | 발신자 |
| To / Cc | Para / CC | À / Cc | 宛先 / CC | 收件人 / 抄送 | 받는사람 / 참조 |
| subject | asunto | objet | 件名 | 主题 | 제목 |
| body | cuerpo | corps | 本文 | 正文 | 본문 |
| backup (noun) | copia de seguridad | sauvegarde | バックアップ | 备份 | 백업 |
| back up (verb) | hacer una copia de seguridad | sauvegarder | バックアップする | 备份 | 백업하다 |
| backup folder | carpeta de copia de seguridad | dossier de sauvegarde | バックアップ フォルダー | 备份文件夹 | 백업 폴더 |
| folder | carpeta | dossier | フォルダー | 文件夹 | 폴더 |
| file name | nombre de archivo | nom de fichier | ファイル名 | 文件名 | 파일명 |
| scan | examinar (button: Examinar) | analyser (button: Analyser) | スキャン | 扫描 | 스캔 |
| cache | caché | cache | キャッシュ | 缓存 | 캐시 |
| normalize file names | normalizar nombres de archivo | normaliser les noms de fichiers | ファイル名の正規化 | 规范化文件名 | 파일명 정규화 |
| normalized | normalizado | normalisé | 正規化済み | 已规范化 | 정규화됨 |
| not normalized | sin normalizar | non normalisé | 未正規化 | 未规范化 | 미정규화 |
| rename | cambiar el nombre | renommer | 名前の変更 | 重命名 | 이름 바꾸기 |
| undo (rename) | deshacer | annuler le renommage | 元に戻す | 撤销 | 되돌리기 |
| Cancel | Cancelar | Annuler | キャンセル | 取消 | 취소 |
| duplicate | duplicado | doublon | 重複 | 重复 | 중복 |
| duplicate (original / keeper) | duplicado (original) | doublon (original) | 重複 (元) | 重复 (原件) | 중복(원본) |
| similar | similar | similaire | 類似 | 相似 | 유사 |
| clean up duplicates | limpiar duplicados | nettoyer les doublons | 重複の整理 | 清理重复项 | 중복 정리 |
| Recycle Bin | Papelera de reciclaje | Corbeille | ごみ箱 | 回收站 | 휴지통 |
| move | mover | déplacer | 移動 | 移动 | 이동 |
| delete | eliminar | supprimer | 削除 | 删除 | 삭제 |
| preview | vista previa | aperçu | プレビュー | 预览 | 미리보기 |
| formatted view / text view | vista con formato / vista de texto | affichage mis en forme / affichage texte | 書式付き表示 / テキスト表示 | 格式视图 / 文本视图 | 서식 보기 / 텍스트 보기 |
| open (in Outlook) | abrir | ouvrir | 開く | 打开 | 열기 |
| show in folder | mostrar en la carpeta | afficher dans le dossier | フォルダーに表示 | 在文件夹中显示 | 폴더에서 보기 |
| search | buscar | rechercher | 検索 | 搜索 | 검색 |
| filter / Reset | filtros / Restablecer | filtres / Réinitialiser | フィルター / リセット | 筛选 / 重置 | 필터 / 초기화 |
| date range | intervalo de fechas | période | 期間 | 日期范围 | 기간 |
| meeting request / accepted / declined / tentative / canceled | solicitud de reunión / aceptada / rechazada / provisional / cancelada | invitation / acceptée / refusée / provisoire / annulée | 会議出席依頼 / 承諾 / 辞退 / 仮承諾 / キャンセル | 会议邀请 / 已接受 / 已拒绝 / 暂定 / 已取消 | 초대 / 수락 / 거절 / 미정 / 취소 |
| error / damaged file | error / archivo dañado | erreur / fichier endommagé | エラー / 破損したファイル | 错误 / 已损坏的文件 | 오류 / 손상된 파일 |
| Settings | Configuración | Paramètres | 設定 | 设置 | 설정 |
| Help | Ayuda | Aide | ヘルプ | 帮助 | 도움말 |
| my addresses | mis direcciones | mes adresses | 自分のアドレス | 我的地址 | 내 주소 |
| Outlook (classic) / new Outlook | Outlook (clásico) / nuevo Outlook | Outlook (classique) / nouvel Outlook | Outlook (クラシック) / 新しい Outlook | Outlook (经典版) / 新版 Outlook | Outlook(classic) / 새 Outlook |
| profile | perfil | profil | プロファイル | 配置文件 | 프로필 |
| run as administrator | ejecutar como administrador | exécuter en tant qu'administrateur | 管理者として実行 | 以管理员身份运行 | 관리자 권한으로 실행 |
| permission level | nivel de permisos | niveau d'autorisation | 権限レベル | 权限级别 | 권한 수준 |
| full-width characters | caracteres de ancho completo | caractères pleine chasse | 全角文字 | 全角字符 | 전각 문자 |
| log file | archivo de registro | fichier journal | ログ ファイル | 日志文件 | 기록 파일 |
| environment check | comprobación del entorno | vérification de l'environnement | 環境チェック | 环境检查 | 환경 점검 |
| screen reader | lector de pantalla | lecteur d'écran | スクリーン リーダー | 屏幕阅读器 | 화면 읽기 프로그램 |
