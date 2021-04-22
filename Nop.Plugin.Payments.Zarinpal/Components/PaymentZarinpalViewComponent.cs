using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Zarinpal
{
    [ViewComponent(Name = "PaymentZarinpal")]
    public class PaymentZarinpalViewComponent : NopViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            return await Task.FromResult(View("~/Plugins/Payments.Zarinpal/Views/PaymentInfo.cshtml"));
        }
    }
}
