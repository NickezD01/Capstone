namespace cpms_Domain
{
    public static class InventoryQuantityRules
    {
        public static bool CanReserve(decimal quantityOnHand, decimal reservedQuantity, decimal requestedQuantity) =>
            requestedQuantity > 0 && quantityOnHand - reservedQuantity >= requestedQuantity;

        public static bool CanIssue(decimal quantityOnHand, decimal reservedQuantity, decimal issueQuantity) =>
            issueQuantity > 0 && quantityOnHand >= issueQuantity && reservedQuantity >= issueQuantity;

        public static bool CanReceive(decimal orderedQuantity, decimal receivedQuantity, decimal receiptQuantity) =>
            receiptQuantity > 0 && receivedQuantity >= 0 && receivedQuantity + receiptQuantity <= orderedQuantity;

        public static bool CanAdjust(decimal quantityOnHand, decimal reservedQuantity, decimal quantityDelta) =>
            quantityDelta != 0 && quantityOnHand + quantityDelta >= reservedQuantity;

        public static bool CanTransfer(decimal quantityOnHand, decimal reservedQuantity, decimal transferQuantity) =>
            transferQuantity > 0 && quantityOnHand - reservedQuantity >= transferQuantity;
    }
}
