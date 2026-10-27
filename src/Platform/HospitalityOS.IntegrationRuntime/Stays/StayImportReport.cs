using HospitalityOS.SharedKernel.Collections;

namespace HospitalityOS.IntegrationRuntime.Stays;

/// <summary>
/// The outcome of one stay import.
/// </summary>
/// <param name="ProviderName">The PMS provider read from.</param>
/// <param name="Imported">The stays applied, in PMS stay ID order.</param>
/// <param name="Skipped">The stays not applied, with reasons.</param>
public sealed record StayImportReport(string ProviderName, ValueList<ImportedStay> Imported, ValueList<SkippedStay> Skipped);
