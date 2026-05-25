using SlimTrack.Models;

namespace SlimTrack.Services;

/// <summary>
/// Serviço de classificação heurístico baseado em palavras-chave.
/// Analisa o título e descrição do chamado para inferir categoria e prioridade.
/// Implementa IClassificationService para que possa ser substituído por um modelo de NLP real.
/// </summary>
public class KeywordClassificationService : IClassificationService
{
    private static readonly Dictionary<TicketCategory, string[]> CategoryKeywords = new()
    {
        [TicketCategory.Hardware] =
        [
            "impressora", "computador", "monitor", "teclado", "mouse", "hardware",
            "memória", "hd", "ssd", "cpu", "processador", "placa", "fonte", "nobreak",
            "scanner", "headset", "webcam", "notebook", "desktop", "cabo", "conector",
            "bateria", "carregador", "pen drive", "pendrive", "usb", "mochila"
        ],
        [TicketCategory.Software] =
        [
            "software", "sistema", "programa", "aplicativo", "app", "windows", "excel",
            "word", "outlook", "instalação", "atualização", "driver", "erro", "falha",
            "bug", "travando", "trava", "lento", "reiniciando", "antivírus", "licença",
            "versão", "patch", "plugin", "extensão", "configuração", "desinstalar"
        ],
        [TicketCategory.Rede] =
        [
            "rede", "internet", "vpn", "wifi", "wi-fi", "sem fio", "conexão", "ping",
            "firewall", "proxy", "ip", "dns", "servidor", "acesso remoto", "roteador",
            "switch", "cabo de rede", "sem conexão", "sem internet", "queda de rede",
            "latência", "largura de banda", "bandwidth", "timeout"
        ],
        [TicketCategory.Acesso] =
        [
            "senha", "login", "acesso", "usuário", "conta", "bloqueado", "permissão",
            "autenticação", "credencial", "token", "certificado", "desbloqueio",
            "expirou", "inativo", "perfil", "grupo", "active directory", "ad", "ldap",
            "dois fatores", "2fa", "mfa", "reset", "redefinir"
        ]
    };

    private static readonly Dictionary<TicketPriority, string[]> PriorityKeywords = new()
    {
        [TicketPriority.Alta] =
        [
            "urgente", "crítico", "parado", "emergência", "não funciona", "impede",
            "produção", "bloqueado", "urgência", "imediato", "grave", "prejudicando",
            "sem acesso", "completamente", "ninguém consegue", "todos afetados",
            "deadline", "prazo", "entrega hoje", "cliente esperando", "parada total"
        ],
        [TicketPriority.Media] =
        [
            "lento", "intermitente", "dificuldade", "parcial", "alguns", "às vezes",
            "eventualmente", "instável", "demora", "demora muito", "não carrega",
            "travou", "reiniciou", "não imprime", "às vezes funciona"
        ],
        [TicketPriority.Baixa] =
        [
            "dúvida", "pergunta", "sugestão", "melhoria", "quando possível",
            "informação", "consulta", "gostaria", "poderia", "verificar se",
            "como faço", "qual é", "orientação"
        ]
    };

    public ClassificationResult Classify(string title, string description)
    {
        var text = $"{title} {description}".ToLowerInvariant();

        // Pontuação por categoria
        var categoryScores = new Dictionary<TicketCategory, int>();
        foreach (var (category, keywords) in CategoryKeywords)
        {
            categoryScores[category] = keywords.Count(kw => text.Contains(kw));
        }

        var bestCategoryEntry = categoryScores.MaxBy(kv => kv.Value);
        var selectedCategory = bestCategoryEntry.Value > 0
            ? bestCategoryEntry.Key
            : TicketCategory.Outros;
        var categoryScore = bestCategoryEntry.Value;
        var totalCategoryScore = categoryScores.Values.Sum();

        // Pontuação por prioridade (hierárquica: Alta > Media > Baixa > default Media)
        var priorityScores = new Dictionary<TicketPriority, int>();
        foreach (var (priority, keywords) in PriorityKeywords)
        {
            priorityScores[priority] = keywords.Count(kw => text.Contains(kw));
        }

        TicketPriority selectedPriority;
        if (priorityScores[TicketPriority.Alta] > 0)
            selectedPriority = TicketPriority.Alta;
        else if (priorityScores[TicketPriority.Media] > 0)
            selectedPriority = TicketPriority.Media;
        else if (priorityScores[TicketPriority.Baixa] > 0)
            selectedPriority = TicketPriority.Baixa;
        else
            selectedPriority = TicketPriority.Media;

        // Confiança: proporção do score da categoria vencedora sobre o total
        var confidence = totalCategoryScore > 0
            ? Math.Min((float)categoryScore / totalCategoryScore, 1.0f)
            : 0.5f;

        // Justificativa textual
        var rationale =
            $"Categoria sugerida: {selectedCategory} (score {categoryScore}). " +
            $"Prioridade sugerida: {selectedPriority} (score {priorityScores[selectedPriority]}).";

        return new ClassificationResult
        {
            Category = selectedCategory,
            Priority = selectedPriority,
            Confidence = confidence,
            Rationale = rationale
        };
    }
}
