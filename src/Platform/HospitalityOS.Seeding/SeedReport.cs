using HospitalityOS.IntegrationRuntime.Stays;

namespace HospitalityOS.Seeding;

/// <summary>
/// The outcome of a seed run.
/// </summary>
/// <param name="Created">Records and integration configurations created by this run.</param>
/// <param name="Existing">Those that already existed and were left as they were.</param>
/// <param name="StayImport">The PMS stay import's report; a stay it skipped is listed with the reason.</param>
public sealed record SeedReport(int Created, int Existing, StayImportReport StayImport);
