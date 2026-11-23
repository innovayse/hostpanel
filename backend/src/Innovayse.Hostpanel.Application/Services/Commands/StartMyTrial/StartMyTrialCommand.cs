namespace Innovayse.Hostpanel.Application.Services.Commands.StartMyTrial;

/// <summary>
/// Command for a client starting the free trial of a product on their own account. Names no account, cycle or
/// amount: the handler resolves the caller and the trial's terms come from the product.
/// </summary>
/// <param name="ProductId">The product whose trial is started.</param>
public sealed record StartMyTrialCommand(int ProductId);
