using System.Globalization;
using RestaurantHandler.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RestaurantHandler.Utilities;

/// <summary>
/// Composes the PDF bill (facture) for a paid <see cref="Order"/>. Reads only the
/// frozen snapshots already on the order/reservations — never recomputes prices.
/// An order is mono-prof: the teacher is the legal seller and BonProf only issues
/// the invoice on his behalf (mandat de facturation). Stored prices are TTC; the HT
/// base and VAT amount are derived here using <see cref="HardCode.VAT_RATE"/>.
/// The order must be loaded with Student.User.Addresses, Teacher.User.Addresses and
/// Reservations.Slot.
/// </summary>
public class InvoiceDocument(Order order) : IDocument
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private static string Money(decimal amount) => amount.ToString("C", Fr);

    // Stored amounts are TTC; HT = TTC / (1 + rate), rounded to the cent.
    private static decimal ToHt(decimal ttc) =>
        decimal.Round(ttc / (1 + HardCode.VAT_RATE), 2);

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingVertical(20).Column(col =>
            {
                col.Item().Element(ComposeParties);
                col.Item().PaddingTop(15).Element(ComposeTable);
            });
            page.Footer().Column(footer =>
            {
                footer.Item().AlignCenter().Text(t =>
                {
                    t.Span("BonProf — facture générée le ");
                    t.Span(MonthRange.ToPlatformTime(DateTimeOffset.UtcNow).ToString("dd/MM/yyyy", Fr));
                });
                footer.Item().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("BonProf").FontSize(20).Bold();
                col.Item().Text("Facture").FontSize(14).SemiBold();
            });

            row.RelativeItem().AlignRight().Column(col =>
            {
                col.Item().Text($"N° {order.OrderNumber}").SemiBold();
                col.Item().Text($"Date d'émission : {order.OrderDate.ToString("dd/MM/yyyy", Fr)}");
            });
        });
    }

    private void ComposeParties(IContainer container)
    {
        var teacher = order.Teacher?.User;
        var student = order.Student?.User;

        container.Column(wrapper =>
        {
            wrapper.Item().Row(row =>
            {
                // Vendeur = le prof (BonProf n'est qu'intermédiaire).
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Vendeur").SemiBold();
                    col.Item().Text(FullName(teacher));
                    foreach (var line in AddressLines(MainAddress(teacher)))
                        col.Item().Text(line);
                    if (order.Teacher?.Siret is { } siret)
                        col.Item().Text($"SIRET : {siret}");
                });

                // Client = l'élève.
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text("Client").SemiBold();
                    col.Item().Text(FullName(student));
                    foreach (var line in AddressLines(MainAddress(student)))
                        col.Item().Text(line);
                    if (!string.IsNullOrWhiteSpace(student?.Email))
                        col.Item().Text(student!.Email);
                });
            });

            wrapper
                .Item()
                .PaddingTop(8)
                .Text("Facture établie par BonProf au nom et pour le compte du vendeur.")
                .FontSize(8)
                .Italic()
                .FontColor(Colors.Grey.Darken1);
        });
    }

    private void ComposeTable(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Désignation
                    columns.RelativeColumn(2); // Créneau (date de prestation)
                    columns.RelativeColumn(1); // Durée (quantité)
                    columns.RelativeColumn(); // P.U. HT
                    columns.RelativeColumn(); // Montant HT
                });

                table.Header(header =>
                {
                    HeaderCell(header, "Désignation");
                    HeaderCell(header, "Créneau");
                    HeaderCell(header, "Durée");
                    HeaderCell(header, "P.U. HT");
                    HeaderCell(header, "Montant HT");
                });

                foreach (var reservation in order.Reservations)
                {
                    var slot = reservation.Slot;
                    var slotLabel = slot is null
                        ? "-"
                        : $"{slot.DateFrom.ToString("dd/MM/yyyy HH:mm", Fr)} - {slot.DateTo.ToString("HH:mm", Fr)}";

                    var hours = slot is null
                        ? 0m
                        : (decimal)(slot.DateTo - slot.DateFrom).Ticks / TimeSpan.TicksPerHour;

                    BodyCell(table, reservation.Cursus?.Name ?? "-");
                    BodyCell(table, slotLabel);
                    BodyCell(table, $"{hours.ToString("0.##", Fr)} h");
                    BodyCell(table, Money(ToHt(reservation.UnitPrice)));
                    BodyCell(table, Money(ToHt(reservation.Amount)));
                }
            });

            // Authoritative total = what the student actually paid (TTC). The HT base
            // and VAT amount are derived from it so the breakdown always reconciles.
            var totalTtc = order.TotalAmount;
            var totalHt = ToHt(totalTtc);
            var vatAmount = totalTtc - totalHt;
            var subtotalHt = order.Reservations.Sum(r => ToHt(r.Amount));

            column.Item().PaddingTop(15).AlignRight().Column(totals =>
            {
                totals.Item().Text($"Sous-total HT : {Money(subtotalHt)}");

                if (order.ReductionPercentage > 0)
                    totals.Item().Text($"Réduction : {order.ReductionPercentage.ToString("0.##", Fr)} %");
                if (order.ReductionAmount > 0)
                    totals.Item().Text($"Réduction : -{Money(order.ReductionAmount)}");

                totals.Item().Text($"Total HT : {Money(totalHt)}");
                totals
                    .Item()
                    .Text($"TVA ({(HardCode.VAT_RATE * 100).ToString("0.##", Fr)} %) : {Money(vatAmount)}");
                totals
                    .Item()
                    .PaddingTop(4)
                    .Text($"Total TTC : {Money(totalTtc)}")
                    .FontSize(13)
                    .Bold();
            });
        });
    }

    private static string FullName(UserApp? user) =>
        user is null ? "-" : $"{user.FirstName} {user.LastName}".Trim();

    // Prefer the main address; fall back to whatever non-archived address exists.
    private static Address? MainAddress(UserApp? user) =>
        user?.Addresses?.FirstOrDefault(a => a.TypeId == HardCode.TYPE_ADDRESS_MAIN)
        ?? user?.Addresses?.FirstOrDefault();

    private static IEnumerable<string> AddressLines(Address? a)
    {
        if (a is null)
            yield break;
        if (!string.IsNullOrWhiteSpace(a.Street))
            yield return a.Street;
        if (!string.IsNullOrWhiteSpace(a.AdditionalInfo))
            yield return a.AdditionalInfo!;
        var cityLine = $"{a.ZipCode} {a.City}".Trim();
        if (!string.IsNullOrWhiteSpace(cityLine))
            yield return cityLine;
        if (!string.IsNullOrWhiteSpace(a.Country))
            yield return a.Country;
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().BorderBottom(1).PaddingVertical(4).Text(text).SemiBold();

    private static void BodyCell(TableDescriptor table, string text) =>
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4).Text(text);
}
