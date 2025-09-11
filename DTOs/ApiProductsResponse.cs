using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;

namespace AgrolandApiSync.DTOs
{
    [XmlRoot("products")]
    public class Products
    {
        [XmlAttribute("elments")]
        public int Elements { get; set; }

        [XmlAttribute("clientid")]
        public int ClientId { get; set; }

        [XmlAttribute("lang")]
        public string Lang { get; set; }

        [XmlAttribute("datetime")]
        public string DateTime { get; set; }

        [XmlAttribute("template")]
        public int Template { get; set; }

        [XmlAttribute("version")]
        public int Version { get; set; }

        [XmlElement("product")]
        public List<Product> ProductList { get; set; }
    }

    public class Product
    {
        private static readonly CultureInfo PlCulture = CultureInfo.GetCultureInfo("pl-PL");

        [XmlAttribute("addedDate")]
        public string AddedDate { get; set; }

        [XmlAttribute("lastUpdate")]
        public string LastUpdate { get; set; }

        [XmlElement("ean")]
        public string Ean { get; set; }

        [XmlElement("id")]
        public int Id { get; set; }

        [XmlElement("sku")]
        public string Sku { get; set; }

        [XmlElement("name")]
        public string Name { get; set; }

        [XmlElement("brand")]
        public Brand Brand { get; set; }

        [XmlElement("desc")]
        public string Desc { get; set; }

        [XmlElement("url")]
        public string Url { get; set; }

        [XmlArray("categories")]
        [XmlArrayItem("category")]
        public List<Category> Categories { get; set; }

        [XmlArray("attributes")]
        [XmlArrayItem("attribute")]
        public List<string> Attributes { get; set; }

        [XmlElement("unit")]
        public string Unit { get; set; }

        // --- Numbers with commas need to be parsed manually ---
        [XmlElement("weight")]
        public string WeightRaw { get; set; }

        [XmlIgnore]
        public decimal? Weight =>
            decimal.TryParse(WeightRaw, NumberStyles.Any, PlCulture, out var v) ? v : (decimal?)null;

        [XmlElement("PKWiU")]
        public string PKWiU { get; set; }

        [XmlElement("inStock")]
        public string InStockRaw { get; set; }

        [XmlIgnore]
        public bool InStock =>
            bool.TryParse(InStockRaw, out var v) ? v :
            (InStockRaw == "True" ? true : InStockRaw == "False" ? false : false);

        [XmlElement("qty")]
        public string QtyRaw { get; set; }

        [XmlIgnore]
        public decimal Qty =>
            decimal.TryParse(QtyRaw, NumberStyles.Any, PlCulture, out var v) ? v : 0;

        [XmlElement("availability")]
        public string Availability { get; set; }

        [XmlElement("requiredBox")]
        public string RequiredBoxRaw { get; set; }

        [XmlIgnore]
        public bool RequiredBox =>
            bool.TryParse(RequiredBoxRaw, out var v) ? v :
            (RequiredBoxRaw == "True" ? true : RequiredBoxRaw == "False" ? false : false);

        [XmlElement("quantityPerBox")]
        public string QuantityPerBoxRaw { get; set; }

        [XmlIgnore]
        public decimal QuantityPerBox =>
            decimal.TryParse(QuantityPerBoxRaw, NumberStyles.Any, PlCulture, out var v) ? v : 0;

        [XmlElement("priceAfterDiscountNet")]
        public string PriceAfterDiscountNetRaw { get; set; }

        [XmlIgnore]
        public decimal PriceAfterDiscountNet =>
            decimal.TryParse(PriceAfterDiscountNetRaw, NumberStyles.Any, PlCulture, out var v) ? v : 0;

        [XmlElement("vat")]
        public int Vat { get; set; }

        [XmlElement("retailPriceGross")]
        public string RetailPriceGrossRaw { get; set; }

        [XmlIgnore]
        public decimal RetailPriceGross =>
            decimal.TryParse(RetailPriceGrossRaw, NumberStyles.Any, PlCulture, out var v) ? v : 0;

        [XmlArray("photos")]
        [XmlArrayItem("photo")]
        public List<Photo> Photos { get; set; }
    }

    public class Brand
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlText]
        public string Name { get; set; }
    }

    public class Category
    {
        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlText]
        public string Name { get; set; }
    }

    public class Photo
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        [XmlAttribute("main")]
        public bool Main { get; set; }

        [XmlText]
        public string Url { get; set; }
    }
}