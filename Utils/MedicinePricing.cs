using System;

namespace DChemist.Utils
{
    public static class MedicinePricing
    {
        public static int TabletsPerBox(int packetsPerBox, int tabletsPerPacket)
        {
            if (packetsPerBox < 1 || tabletsPerPacket < 1)
                throw new ArgumentOutOfRangeException(nameof(packetsPerBox), "Packaging counts must be at least 1.");
            return checked(packetsPerBox * tabletsPerPacket);
        }

        public static decimal ToTabletPrice(decimal price, bool boxMode, int tabletsPerBox) =>
            boxMode ? price / tabletsPerBox : price;

        public static decimal ToEntryPrice(decimal tabletPrice, bool boxMode, int tabletsPerBox) =>
            boxMode ? tabletPrice * tabletsPerBox : tabletPrice;
    }
}
