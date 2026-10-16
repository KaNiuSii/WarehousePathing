using System.Collections.Generic;

namespace Engine.Picking;

/// <summary>Pozycja zamówienia: ile sztuk danego produktu (nazwa jak w Item.Name) trzeba zebrać.</summary>
public record OrderLine(string Product, int Quantity);

/// <summary>
/// Zlecenie kompletacji.
/// CartCapacity - ile sztuk (łącznie) mieści wózek na jeden kurs.
/// Target - komórka-ścieżka, z której zaczyna się każdy kurs i do której wózek wraca z towarem.
/// </summary>
public record PickingRequest(
    IReadOnlyList<OrderLine> Lines,
    int CartCapacity,
    (int X, int Y) Target);