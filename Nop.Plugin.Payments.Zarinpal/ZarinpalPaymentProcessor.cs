using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.Zarinpal.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.Payments.Zarinpal
{
    /// <summary>
    /// Zarinpal payment processor
    /// </summary>
    public class ZarinPalPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Constants

        public static readonly HttpClient ClientZarinPal = new HttpClient();

        #endregion

        #region Fields
        private readonly CustomerSettings _customerSettings;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPaymentService _paymentService;
        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly ZarinpalPaymentSettings _zarinPalPaymentSettings;
        private readonly ILanguageService _languageService;
        private readonly IStoreService _storeService;
        private readonly ICustomerService _customerService;
        private IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IAddressService _addressService;


        #endregion

        #region Ctor

        public ZarinPalPaymentProcessor
        (
            IWebHelper webHelper,
            ITaxService taxService,
            IWorkContext workContext,
            IStoreContext storeContext,
            IStoreService storeService,
            IAddressService addressService,
            ISettingService settingService,
            IPaymentService paymentService,
            ICustomerService customerService,
            ICurrencyService currencyService,
            ILanguageService languageService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            ICheckoutAttributeParser checkoutAttributeParser,
            IGenericAttributeService genericAttributeService,
            IOrderTotalCalculationService orderTotalCalculationService,
            CustomerSettings customerSettings,
            CurrencySettings currencySettings,
            ZarinpalPaymentSettings zarinPalPaymentSettings
        )
        {
            _paymentService = paymentService;
            _httpContextAccessor = httpContextAccessor;
            _workContext = workContext;
            _customerService = customerService;
            _storeService = storeService;
            _currencySettings = currencySettings;
            _checkoutAttributeParser = checkoutAttributeParser;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _localizationService = localizationService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _settingService = settingService;
            _taxService = taxService;
            _webHelper = webHelper;
            _zarinPalPaymentSettings = zarinPalPaymentSettings;
            _storeContext = storeContext;
            _languageService = languageService;
            _customerSettings = customerSettings;
            _addressService = addressService;
        }

        #endregion

        #region Methods

        public override async Task InstallAsync()
        {
            //Logic during installation goes here...

            //settings
            var settings = new ZarinpalPaymentSettings
            {
                UseSandbox = true,
                MerchantID = "99999999-9999-9999-9999-999999999999",
                BlockOverseas = false,
                RialToToman = true,
                Method = EnumMethod.REST,
                UseZarinGate = false,
                ZarinGateType = EnumZarinGate.ZarinGate
            };
            await _settingService.SaveSettingAsync(settings);

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Use", "Use ZarinGate");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Use", "استفاده از زرین گیت", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Type", "Select ZarinGate Type");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Type", "انتخاب نوع زرین گیت", languageCulture: "fa-IR");

            var zarinGateLink = "https://www.zarinpal.com/blog/زرین-گیت،-درگاهی-اختصاصی-به-نام-وبسایت/";
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Instructions", $"Read About the <a href=\"{zarinGateLink}\">Zarin Gate</a> Then Select the ZarinGateLink type from below :");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Instructions",
             string.Concat("لطفا اول شرایط استفاده از زرین گیت را در ", $"<a href=\"{zarinGateLink}\"> در این قسمت </a>", "مطالعه نموده و سپس نوع آن را انتخاب نمایید")
            , languageCulture: "fa-IR");


            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.Method", "Communication Method");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.Method", "روش پرداخت", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.Method.REST", "REST(recommanded)");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.Method.SOAP", "SOAP");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.UseSandbox", "Use Snadbox for testing payment GateWay without real paying.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.UseSandbox", "تست درگاه زرین پال بدون پرداخت هزینه", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.MerchantID", "GateWay Merchant ID");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.MerchantID", "کد پذیرنده", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Instructions",
            string.Concat("You can use Zarinpal.com GateWay as a payment gateway. Zarinpal is not a bank but it is an interface which customers can pay with.",
             "<br/>", "Please consider that if you leave MerchantId field empty the Zarinpal Gateway will be hidden and not choosable when checking out"));
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.ZarinPal.Instructions",
             string.Concat("شما می توانید از زرین پال به عنوان یک درگاه پرداخت استفاده نمایید، زرین پال یک بانک نیست بلکه یک واسط بانکی است که کاربران میتوانند از طریق آن مبلغ مورد نظر را پرداخت نمایند، باید آگاه باشید که درگاه زرین پال درصدی از پول پرداخت شده کاربران را به عنوان کارمزد دریافت میکند.",
            "<br/>", "توجه داشته باشید که اگر فیلد کد پذیرنده خالی باشد درگاه زرین پال در هنگام پرداخت مخفی می شود و قابل انتخاب نیست"), languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("plugins.payments.zarinpal.PaymentMethodDescription", "ZarinPal, The Bank Interface");
            await _localizationService.AddOrUpdateLocaleResourceAsync("plugins.payments.zarinpal.PaymentMethodDescription", "درگاه واسط زرین پال", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RedirectionTip", "You will be redirected to ZarinPal site to complete the order.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RedirectionTip", "هم اکنون به درگاه بانک زرین پال منتقل می شوید.", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.BlockOverseas", "Block oversease access (block non Iranians)");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.BlockOverseas", "قطع دسترسی برای آی پی های خارج از کشور", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RialToToman", "Convert Rial To Toman");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RialToToman", "تبدیل ریال به تومن", languageCulture: "fa-IR");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RialToToman.Instructions",
            string.Concat(
                "The default currency of zarinpal is Toman", "<br/>",
                "Therefore if your website uses Rial before paying it should be converted to Toman", "<br/>",
                "please consider that to convert Rial to Toman system divides total to 10, so the last digit will be removed", "<br/>",
                "To do the stuff check this option"
            ));
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RialToToman.Instructions",
            string.Concat(
                "واحد ارزی پیش فرض درگاه پرداخت زرین پال تومان می باشد.", "<br/>",
                "لذا در صورتی که وبسایت شما از واحد ارزی ریال استفاده می کند باید قبل از پرداخت مبلغ نهایی به تومان تبدیل گردد", "<br/>",
                "لطفا در نظر داشته باشید که جهت تبدیل ریال به تومان عدد تقسیم بر 10 شده و در واقع رقم آخر حذف می گردد", "<br/>",
                "در صورتی که مایل به تغییر از ریال به تومان هنگام پرداخت می باشید این گزینه را فعال نمایید"
            ), languageCulture: "fa-IR");

            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            //Logic during uninstallation goes here...

            //settings
            await _settingService.DeleteSettingAsync<ZarinpalPaymentSettings>();

            //locales

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Use");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Type");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.ZarinGate.Instructions");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.Method");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.Method.SOAP");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.Method.REST");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.UseSandbox");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.ZarinPal.Fields.MerchantID");
            await _localizationService.DeleteLocaleResourceAsync("plugins.payments.zarinpal.PaymentMethodDescription");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.ZarinPal.Instructions");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RedirectionTip");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.BlockOverseas");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RialToToman");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.Zarinpal.Fields.RialToToman.Instructions");

            await base.UninstallAsync();
        }

        public string GetPublicViewComponentName()
        {
            //return $"{_webHelper.GetStoreLocation()}Plugin/Payments.Zarinpal/Views/Configure";
            return "PaymentZarinpal";
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/Paymentzarinpal/Configure";
        }

        public Task ManageSiteMapAsync(SiteMapNode rootNode)
        {
            var nopTopPluginsNode = rootNode.ChildNodes.FirstOrDefault(x => x.SystemName == "Nop");
            if (nopTopPluginsNode == null)
            {
                nopTopPluginsNode = new SiteMapNode()
                {
                    SystemName = "Nop",
                    Title = "Nop",
                    Visible = true,
                    IconClass = "fa-gear"
                };
                rootNode.ChildNodes.Add(nopTopPluginsNode);
            }

            var menueLikeProduct = new SiteMapNode()
            {
                SystemName = "ZarinPal",
                Title = "ZarinPal Configuration",
                ControllerName = "PaymentZarinPal",
                ActionName = "Configure",
                Visible = true,
                IconClass = "fa-dot-circle-o",
                RouteValues = new RouteValueDictionary() { { "Area", "Admin" } },
            };

            nopTopPluginsNode.ChildNodes.Add(menueLikeProduct);

            return Task.CompletedTask;
        }

        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult((IList<string>)new List<string>());
        }

        public async Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            bool hide = false;
            var zarinPalPaymentSettings = await _settingService.LoadSettingAsync<ZarinpalPaymentSettings>(await _storeContext.GetActiveStoreScopeConfigurationAsync());
            hide = string.IsNullOrWhiteSpace(_zarinPalPaymentSettings.MerchantID);
            if (_zarinPalPaymentSettings.BlockOverseas)
                hide = hide || ZarinpalHelper.isOverseaseIp(_httpContextAccessor.HttpContext.Connection.RemoteIpAddress);
            return hide;
        }

        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return Task.FromResult(new ProcessPaymentRequest());
        }

        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return Task.FromResult(result);
        }

        public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(decimal.Zero);
        }

        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return Task.FromResult(result);
        }

        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return Task.FromResult(result);
        }

        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            Order order = postProcessPaymentRequest.Order;
            Customer customer = await _customerService.GetCustomerByIdAsync(postProcessPaymentRequest.Order.CustomerId);

            var total = Convert.ToInt32(Math.Round(order.OrderTotal, 2));
            if (_zarinPalPaymentSettings.RialToToman)
                total = total / 10;

            string phoneOfUser = String.Empty;
            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);
            var shippingAddress = await _addressService.GetAddressByIdAsync(order.ShippingAddressId ?? 0);

            if (_customerSettings.PhoneEnabled)// Default Phone number of the Customer
                phoneOfUser = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.PhoneAttribute);
            if (string.IsNullOrEmpty(phoneOfUser))//Phone number of the BillingAddress
                phoneOfUser = billingAddress.PhoneNumber;
            if (string.IsNullOrEmpty(phoneOfUser))//Phone number of the ShippingAddress
                phoneOfUser = string.IsNullOrEmpty(shippingAddress?.PhoneNumber) ? phoneOfUser : $"{phoneOfUser} - {shippingAddress.PhoneNumber}";

            var name = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.FirstNameAttribute);
            var family = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.LastNameAttribute);

            string fullName = $"{name ?? ""} {family ?? ""}".Trim();
            string urlToRedirect = "";
            string zarinGate = _zarinPalPaymentSettings.UseZarinGate ? _zarinPalPaymentSettings.ZarinGateType.ToString() : null;
            //string description = $"{_storeService.GetStoreByIdAsync(order.StoreId).Name}{(string.IsNullOrEmpty(NameFamily) ? "" : $" - {NameFamily}")} - {customer.Email}{(string.IsNullOrEmpty(phoneOfUser) ? "" : $" - {phoneOfUser}")}";
            string description = "asdsadsadsad";
            string callbackURL = string.Concat(_webHelper.GetStoreLocation(), "Plugins/PaymentZarinpal/ResultHandler", "?OGUId=" + postProcessPaymentRequest.Order.OrderGuid);
            string storeAddress = _webHelper.GetStoreLocation();

            if (_zarinPalPaymentSettings.Method == EnumMethod.SOAP)
            {
                if (_zarinPalPaymentSettings.UseSandbox)
                    using (ServiceReferenceZarinpalSandBox.PaymentGatewayImplementationServicePortTypeClient zpalSr = new ServiceReferenceZarinpalSandBox.PaymentGatewayImplementationServicePortTypeClient())
                    {
                        ServiceReferenceZarinpalSandBox.PaymentRequestResponse resp = zpalSr.PaymentRequestAsync(
                                _zarinPalPaymentSettings.MerchantID,
                                total,
                                description,
                                customer.Email,
                                phoneOfUser,
                                callbackURL
                            ).Result;

                        urlToRedirect = ZarinpalHelper.ProduceRedirectUrl(storeAddress,
                                           resp.Body.Status,
                                           _zarinPalPaymentSettings.UseSandbox,
                                           resp.Body.Authority,
                                           zarinGate);
                    }
                else
                    using (ServiceReferenceZarinpal.PaymentGatewayImplementationServicePortTypeClient zpalSr = new ServiceReferenceZarinpal.PaymentGatewayImplementationServicePortTypeClient())
                    {
                        var resp = zpalSr.PaymentRequestAsync
                               (
                                   _zarinPalPaymentSettings.MerchantID,
                                   total,
                                   description,
                                   customer.Email,
                                   phoneOfUser,
                                   callbackURL
                               ).Result;


                        urlToRedirect = ZarinpalHelper.ProduceRedirectUrl
                               (
                                   storeAddress,
                                   resp.Body.Status,
                                   _zarinPalPaymentSettings.UseSandbox,
                                   resp.Body.Authority,
                                   zarinGate
                               );
                    }
            }
            else if (_zarinPalPaymentSettings.Method == EnumMethod.REST)
            {
                var url = $"https://{(_zarinPalPaymentSettings.UseSandbox ? "sandbox" : "www")}.zarinpal.com/pg/rest/WebGate/PaymentRequest.json";

                var values = new Dictionary<string, string>
                {
                    { "MerchantID", _zarinPalPaymentSettings.MerchantID }, //Change This To work, some thing like this : xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
                    { "Amount", total.ToString() }, //Toman
                    { "CallbackURL", callbackURL },
                    { "Mobile", phoneOfUser },
                    { "Email", customer.Email },
                    { "Description", description }
                };

                var paymentRequestJsonValue = JsonConvert.SerializeObject(values);
                var content = new StringContent(paymentRequestJsonValue, Encoding.UTF8, "application/json");

                var response = ClientZarinPal.PostAsync(url, content).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;

                var restRequestModel = JsonConvert.DeserializeObject<RestRequestModel>(responseString);

                urlToRedirect = ZarinpalHelper.ProduceRedirectUrl(storeAddress,
                    restRequestModel?.Status,
                    _zarinPalPaymentSettings.UseSandbox,
                    restRequestModel.Authority,
                    zarinGate);
            }

            var uri = new Uri(urlToRedirect);
            _httpContextAccessor.HttpContext.Response.Redirect(uri.AbsoluteUri);
        }

        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return await Task.FromResult(result);
        }

        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }


        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayPal site to complete the payment"
            get { return _localizationService.GetResourceAsync("plugins.payments.zarinpal.PaymentMethodDescription").ToString(); }
        }

        #endregion


        public Task<string> GetPaymentMethodDescriptionAsync()
        {
            return Task.FromResult("sadasdasdsadasdasdasdasdasd");
        }



    }
}
