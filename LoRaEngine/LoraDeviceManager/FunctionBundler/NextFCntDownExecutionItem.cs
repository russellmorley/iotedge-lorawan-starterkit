// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraDeviceManager.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.FunctionBundler;

    public class NextFCntDownExecutionItem : IFunctionBundlerExecutionItem
    {
        private readonly ILoraDeviceManager loraDeviceManager;

        public NextFCntDownExecutionItem(ILoraDeviceManager loraDeviceManager)
        {
            this.loraDeviceManager = loraDeviceManager;
        }

        public async Task<FunctionBundlerExecutionState> ExecuteAsync(IPipelineExecutionContext context)
        {
            if (context is null) throw new System.ArgumentNullException(nameof(context));

            if (context.Result.AdrResult?.FCntDown != null)
            {
                // adr already processed the next fcnt down check
                context.Result.NextFCntDown = context.Result.AdrResult.FCntDown;
                return FunctionBundlerExecutionState.Continue;
            }

            if (!context.Result.NextFCntDown.HasValue)
            {
                var next = await loraDeviceManager.GetNextFCntDownAsync(context.DevEUI, context.Request.GatewayId, context.Request.ClientFCntUp, context.Request.ClientFCntDown);
                context.Result.NextFCntDown = next;
            }

            return FunctionBundlerExecutionState.Continue;
        }

        public int Priority => 3;

        public bool NeedsToExecute(FunctionBundlerItemType item)
        {
            return (item & FunctionBundlerItemType.FCntDown) == FunctionBundlerItemType.FCntDown;
        }

        public Task OnAbortExecutionAsync(IPipelineExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
