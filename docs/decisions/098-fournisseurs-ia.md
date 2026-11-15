<!-- 098-fournisseurs-ia.md -->
<!-- The providers of the assistant (3.2): the great makers and the models of the machine beside Anthropic and AWS Bedrock, ranged by where the data go, one connector of the Chat Completions API read in a table of the providers, the only addresses written in the shipped code -->

# 098 - Les fournisseurs de l'assistant IA

Statut : acceptée le 11/11/2026 (3.2), sur demande du porteur du projet : « Le menu paramétrage pour configurer la
connexion à l'IA propose anthropic et aws... Il faut ajouter les plus grands : OpenAI, Google/Gemini, X/Grok, Kimi et
VIBE. Je te laisse corriger et faire le tri pour afficher par famille logique. Mais je veux tous les voir. Si j'ai
oublié un majeur, ajoute-le. » **Révisée le 12/11/2026**, avant toute livraison, après une relecture critique : le
transport du connecteur et le nom du plafond (point 2), la boucle locale par son adresse (point 3), Qwen en objet
JSON (point 4), la clé d'Azure liée à sa ressource (point 6), le coût estimé pour Claude par ses deux connecteurs
(point 7).

## Contexte

L'ADR 077 a ouvert une seule exception à « aucun appel sortant » (D14) : l'assistant, éteint par défaut, par l'API
d'Anthropic ou AWS Bedrock, choisis par un administrateur. Deux fournisseurs ressemblent à la configuration d'une
entreprise donnée ; une entreprise choisit souvent son fournisseur d'IA par contrat ou par le lieu de ses données.
« VIBE » est Mistral Vibe, la plateforme de Mistral AI qui remplace Le Chat depuis mai 2026 ; son API est celle de
Mistral.

## Décision

1. **Les fournisseurs, rangés par lieu des données** (ordre de l'énumération `AssistantConnector`, qui est l'ordre de la
   liste) :
   - sans connexion : aucun (révision du 13/11/2026, lot J10, demande du porteur du projet : un seul choix ; il fait
     ce que faisait le copier-coller, la consigne copiée dans une IA et sa réponse collée, rien ne part ; un réglage
     gardé en copier-coller est toujours lu et accepté, et montré comme « Aucun ») ;
   - sur ce poste ou ce serveur (rien ne sort) : Ollama, LM Studio ;
   - clouds d'entreprise : AWS Bedrock, Microsoft Azure OpenAI ;
   - éditeurs américains : Anthropic, OpenAI, Google (Gemini), xAI (Grok) ;
   - éditeur européen : Mistral AI (Vibe) ;
   - éditeurs chinois : DeepSeek, Moonshot AI (Kimi), Alibaba Cloud (Qwen).
   Chaque famille dit où partent les données ; les éditeurs chinois sont nommés comme tels.
2. **Un connecteur pour l'API Chat Completions** (`OpenAiCompatibleConnector`) : tous les nouveaux fournisseurs la
   parlent ; aucun paquet nouveau (`HttpClient`, `System.Text.Json`) ; une instance par fournisseur, lue dans la table
   `AssistantProviders` (adresse, en-tête de la clé, forme de la sortie structurée, nom du plafond). Le **nom du
   plafond** est écrit une fois par fournisseur, et un test le fige : `max_completion_tokens` pour OpenAI et Azure
   OpenAI, `max_tokens` pour les autres. Son **gestionnaire HTTP** ne suit aucune redirection, comme les deux
   connecteurs de Claude : une réponse 3xx est un échec (`unreachable`), rien n'est rejoué vers un autre hôte. Il lit
   la réponse jusqu'à 8 192 000 octets (64 octets par jeton du plus grand plafond ; au-delà, ou sans la forme d'une
   complétion, l'échec est `invalid_reply`) et un refus jusqu'à 64 Kio. Il n'emploie le proxy du système que pour les
   fournisseurs en ligne : Ollama et LM Studio sont appelés sans lui. « Tester la connexion » envoie le plafond
   réglé, et un plafond que le fournisseur refuse a son code, `ceiling_refused`. Le message d'un fournisseur est
   relayé sans la clé ni un mot qui en porte une part.
3. **Les adresses ne sont écrites qu'à un endroit** : la table des fournisseurs, dans `src/Topogenia.Assistant/`. La
   règle `Shipped_code_calls_no_external_address` admet ce seul fichier, y refuse toute adresse hors HTTPS sauf la
   boucle locale par son adresse littérale, et un test compare ses hôtes à la liste de `docs/securite.md`. Azure est
   joint à la ressource des réglages (`<ressource>.openai.azure.com`, le nom seul saisi et contrôlé), Ollama et LM
   Studio sur `127.0.0.1` au port des réglages : **aucune adresse n'est saisie** (S-5.4-13). Un modèle sur un autre
   poste du réseau n'est pas offert.
4. **La réponse** : forcée par le schéma de la réponse (`response_format` `json_schema` strict) ; DeepSeek, qui n'a que
   le mode objet JSON, et Qwen, appelé de même (`json_object`), reçoivent le schéma dans les instructions ; Kimi reste
   en `json_schema`. L'application relit toute réponse et renvoie ses fautes une fois, comme pour Claude.
5. **Les fichiers joints** : Word, Excel et Visio en texte pour tous ; les images pour Claude, OpenAI, Azure, Gemini,
   Grok et Mistral ; les PDF tels quels pour Claude et OpenAI. Le panneau n'offre que ce que le modèle lit, l'hôte refuse
   le reste.
6. **Le modèle est saisi** pour tout fournisseur autre qu'Anthropic : les noms changent souvent ; le dialogue en
   propose quelques-uns (les alias stables de Mistral, DeepSeek et Qwen, les modèles qu'OpenAI et Gemini nommaient le
   11/11/2026), aucun ailleurs (Grok, Kimi, Azure, modèles locaux). La clé est gardée comme celle d'Anthropic
   (protection des données d'ASP.NET Core) ; elle fait 8 caractères au moins. Une clé gardée appartient à son
   fournisseur, et celle d'Azure à sa ressource aussi : une autre ressource enregistrée sans clé saisie l'efface.
7. **Le coût** n'est estimé que pour Claude, aux prix publics de l'API d'Anthropic, par ses deux connecteurs : l'API
   d'Anthropic et AWS Bedrock. Les autres fournisseurs montrent leurs jetons.

## Écarts

- Google Vertex AI : son identité Google Cloud demande un SDK et une connexion de plus ; Gemini par sa clé couvre la
  famille Google.
- OpenRouter et les passerelles : elles renvoient vers des dizaines d'éditeurs, le lieu des données n'est plus nommé.

## Conséquences

- Une migration ajoute la ressource et le port et élargit la contrainte des connecteurs.
- `docs/securite.md`, les guides d'exploitation (destinations sortantes), le manuel et l'aide nomment les fournisseurs.
- Tests : connecteur par un faux gestionnaire HTTP (adresse, clé, corps, schéma, lecture, erreurs, reprises), règles
  des réglages, fichiers par fournisseur, liste du dialogue par familles, règle des adresses du code livré.
