using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Zarinpal;
using Nop.Plugin.Payments.Zarinpal.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using ServiceReferenceZarinpal;

namespace Nop.Plugin.Payments.Controllers
{
    public class PaymentZarinPalController : BasePaymentController
    {
        #region Fields
        private readonly IPaymentService _paymentService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly ZarinpalPaymentSettings _zarinPalPaymentSettings;

        #endregion

        #region Ctor
        public PaymentZarinPalController
        (
            ILogger logger,
            IWebHelper webHelper,
            IWorkContext workContext,
            IStoreContext storeContext,
            IOrderService orderService,
            IPaymentService paymentService,
            ISettingService settingService,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            INotificationService notificationService,
            IPaymentPluginManager paymentPluginManager,
            IOrderProcessingService orderProcessingService,
            IGenericAttributeService genericAttributeService,
            ShoppingCartSettings shoppingCartSettings,
            ZarinpalPaymentSettings zarinPalPaymentSettings
        )
        {
            _logger = logger;
            _webHelper = webHelper;
            _workContext = workContext;
            _storeContext = storeContext;
            _orderService = orderService;
            _paymentService = paymentService;
            _settingService = settingService;
            _permissionService = permissionService;
            _localizationService = localizationService;
            _notificationService = notificationService;
            _paymentPluginManager = paymentPluginManager;
            _shoppingCartSettings = shoppingCartSettings;
            _orderProcessingService = orderProcessingService;
            _genericAttributeService = genericAttributeService;
            _zarinPalPaymentSettings = zarinPalPaymentSettings;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var zarinPalPaymentSettings = await _settingService.LoadSettingAsync<ZarinpalPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = zarinPalPaymentSettings.UseSandbox,
                MerchantID = zarinPalPaymentSettings.MerchantID,
                BlockOverseas = zarinPalPaymentSettings.BlockOverseas,
                RialToToman = zarinPalPaymentSettings.RialToToman,
                Method = zarinPalPaymentSettings.Method,
                UseZarinGate = zarinPalPaymentSettings.UseZarinGate,
                ZarinGateType = zarinPalPaymentSettings.ZarinGateType
            };

            if (storeScope <= 0)
                return View("~/Plugins/Payments.Zarinpal/Views/Configure.cshtml", model);

            model.UseSandbox_OverrideForStore = await _settingService.SettingExistsAsync(zarinPalPaymentSettings, x => x.UseSandbox, storeScope);
            model.MerchantID_OverrideForStore = await _settingService.SettingExistsAsync(zarinPalPaymentSettings, x => x.MerchantID, storeScope);
            model.BlockOverseas_OverrideForStore = await _settingService.SettingExistsAsync(zarinPalPaymentSettings, x => x.BlockOverseas, storeScope);
            model.RialToToman_OverrideForStore = await _settingService.SettingExistsAsync(zarinPalPaymentSettings, x => x.RialToToman, storeScope);
            model.Method_OverrideForStore = await _settingService.SettingExistsAsync(zarinPalPaymentSettings, x => x.Method, storeScope);
            model.UseZarinGate_OverrideForStore = await _settingService.SettingExistsAsync(zarinPalPaymentSettings, x => x.UseZarinGate, storeScope);
            model.ZarinGateType_OverrideForStore = await _settingService.SettingExistsAsync(zarinPalPaymentSettings, x => x.ZarinGateType, storeScope);

            return View("~/Plugins/Payments.Zarinpal/Views/Configure.cshtml", model);
        }


        [HttpPost]
        [AuthorizeAdmin]
        //[AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var zarinPalPaymentSettings = await _settingService.LoadSettingAsync<ZarinpalPaymentSettings>(storeScope);

            //save settings
            zarinPalPaymentSettings.UseSandbox = model.UseSandbox;
            zarinPalPaymentSettings.MerchantID = model.MerchantID;
            zarinPalPaymentSettings.BlockOverseas = model.BlockOverseas;
            zarinPalPaymentSettings.RialToToman = model.RialToToman;
            zarinPalPaymentSettings.Method = model.Method;
            zarinPalPaymentSettings.UseZarinGate = model.UseZarinGate;
            zarinPalPaymentSettings.ZarinGateType = model.ZarinGateType;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            await _settingService.SaveSettingOverridablePerStoreAsync(zarinPalPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(zarinPalPaymentSettings, x => x.MerchantID, model.MerchantID_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(zarinPalPaymentSettings, x => x.BlockOverseas, model.MerchantID_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(zarinPalPaymentSettings, x => x.RialToToman, model.RialToToman_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(zarinPalPaymentSettings, x => x.Method, model.Method_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(zarinPalPaymentSettings, x => x.UseZarinGate, model.UseZarinGate_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(zarinPalPaymentSettings, x => x.ZarinGateType, model.ZarinGateType_OverrideForStore, storeScope, false);


            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        #endregion

        public async Task<ActionResult> ResultHandler(string status, string authority, string oGUID)
        {
            if (!(await _paymentPluginManager.LoadPluginBySystemNameAsync("Payments.Zarinpal") is ZarinPalPaymentProcessor processor) || !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("ZarinPal module cannot be loaded");

            var orderNumberGuid = Guid.Empty;
            try
            {
                orderNumberGuid = new Guid(oGUID);
            }
            catch { }

            var order = await _orderService.GetOrderByGuidAsync(orderNumberGuid);
            var total = Convert.ToInt32(Math.Round(order.OrderTotal, 2));
            if (_zarinPalPaymentSettings.RialToToman)
                total = total / 10;


            if (string.IsNullOrEmpty(status) == false && string.IsNullOrEmpty(authority) == false)
            {
                var refId = "0";
                System.Net.ServicePointManager.Expect100Continue = false;
                var statusCode = -1;
                var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
                var zarinPalSettings = await _settingService.LoadSettingAsync<ZarinpalPaymentSettings>(storeScope);

                if (_zarinPalPaymentSettings.Method == EnumMethod.SOAP)
                {
                    if (_zarinPalPaymentSettings.UseSandbox)
                        using (PaymentGatewayImplementationServicePortTypeClient zpalSr = new PaymentGatewayImplementationServicePortTypeClient())
                        {
                            var res = zpalSr.PaymentVerificationAsync
                                 (
                                    zarinPalSettings.MerchantID,
                                    authority,
                                    total
                                 ).Result; //test

                            statusCode = res.Body.Status;
                            refId = res.Body.RefID.ToString();
                        }
                    else
                        using (PaymentGatewayImplementationServicePortTypeClient zpalSr = new ServiceReferenceZarinpal.PaymentGatewayImplementationServicePortTypeClient())
                        {
                            var res = zpalSr.PaymentVerificationAsync(
                                zarinPalSettings.MerchantID,
                                authority,
                                total).Result;
                            statusCode = res.Body.Status;
                            refId = res.Body.RefID.ToString();
                        }
                }
                else if (_zarinPalPaymentSettings.Method == EnumMethod.REST)
                {
                    var url = $"https://{(_zarinPalPaymentSettings.UseSandbox ? "sandbox" : "www")}.zarinpal.com/pg/rest/WebGate/PaymentVerification.json";
                    var values = new Dictionary<string, string>
                        {
                            { "MerchantID", zarinPalSettings.MerchantID },
                            { "Authority", authority },
                            { "Amount", total.ToString() } //Toman
                        };

                    var paymenResponsetJsonValue = JsonConvert.SerializeObject(values);
                    var content = new StringContent(paymenResponsetJsonValue, Encoding.UTF8, "application/json");

                    var response = ZarinPalPaymentProcessor.ClientZarinPal.PostAsync(url, content).Result;
                    var responseString = response.Content.ReadAsStringAsync().Result;

                    var restVerifyModel =
                    JsonConvert.DeserializeObject<RestVerifyModel>(responseString);
                    statusCode = restVerifyModel.Status;
                    refId = restVerifyModel.RefID;
                }

                var result = ZarinpalHelper.StatusToMessage(statusCode);

                var orderNote = new OrderNote()
                {
                    OrderId = order.Id,
                    Note = string.Concat(
                     "پرداخت ",
                    (result.IsOk ? "" : "نا"), "موفق", " - ",
                        "پیغام درگاه : ", result.Message,
                      result.IsOk ? string.Concat(" - ", "کد پی گیری : ", refId) : ""
                      ),
                    DisplayToCustomer = true,
                    CreatedOnUtc = DateTime.UtcNow
                };

                await _orderService.InsertOrderNoteAsync(orderNote);

                if (result.IsOk && _orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    order.AuthorizationTransactionId = refId;
                    await _orderService.UpdateOrderAsync(order);
                    await _orderProcessingService.MarkOrderAsPaidAsync(order);
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                }
            }
            return RedirectToRoute("orderdetails", new { orderId = order.Id });
        }

        public ActionResult ErrorHandler(string error)
        {
            var code = 0;
            Int32.TryParse(error, out code);
            if (code != 0)
                error = ZarinpalHelper.StatusToMessage(code).Message;
            ViewBag.Err = string.Concat("خطا : ", error);
            return View("~/Plugins/Payments.Zarinpal/Views/ErrorHandler.cshtml");
        }
    }
}