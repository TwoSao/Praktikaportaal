namespace Praktikaportaal.Web.Services;

/// <summary>
/// The API's business messages are English. The site shows Estonian: known messages are translated here,
/// anything unknown falls back to the API's own text so no information is lost.
/// </summary>
public static class ApiMessages
{
    private static readonly (string Contains, string Estonian)[] Translations =
    [
        ("already have an active application", "Sul on sellele praktikakohale juba aktiivne avaldus."),
        ("deadline for this internship has passed", "Avalduste esitamise tähtaeg on möödas."),
        ("not currently accepting applications", "See praktikakoht ei võta praegu avaldusi vastu."),
        ("cannot be withdrawn", "Seda avaldust ei saa enam tagasi võtta."),
        ("User with this email already exists", "Selle e-posti aadressiga kasutaja on juba olemas."),
        ("Account is deactivated", "Konto on deaktiveeritud. Võta ühendust toega."),
        ("Only PDF (.pdf) and Word (.docx)", "Lubatud on ainult PDF- (.pdf) ja Word-failid (.docx)."),
        ("is not a valid .pdf document", "Faili sisu ei ole korrektne PDF-dokument."),
        ("is not a valid .docx document", "Faili sisu ei ole korrektne DOCX-dokument."),
        ("does not match a", "Failitüüp ei vasta faililaiendile."),
        ("file is too large", "Fail on liiga suur. Maksimaalne suurus on 5 MB."),
        ("uploaded file is empty", "Üleslaaditud fail on tühi."),
        ("Cannot change application status", "Avalduse staatust ei saa sellele väärtusele muuta."),
        ("Unknown skill", "Valitud oskust ei leitud."),
        ("is listed more than once", "Sama oskus on valitud mitu korda."),
        ("End date must be after start date", "Lõppkuupäev peab olema pärast alguskuupäeva."),
        ("deadline cannot be in the past", "Avalduste tähtaeg ei tohi olla minevikus."),
        ("deadline must be before the start date", "Avalduste tähtaeg peab olema enne praktika algust."),
    ];

    /// <summary>The message to show the user for an API error.</summary>
    public static string UserMessage(this ApiException ex) => Translate(ex.Detail ?? ex.Title);

    public static string Translate(string apiMessage)
    {
        foreach (var (contains, estonian) in Translations)
        {
            if (apiMessage.Contains(contains, StringComparison.OrdinalIgnoreCase))
                return estonian;
        }

        return apiMessage;
    }
}
