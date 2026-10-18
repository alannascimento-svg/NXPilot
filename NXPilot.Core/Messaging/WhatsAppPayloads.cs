// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Messaging;

/// <summary>
/// Payloads no formato documentado do webhook da Cloud API (campo "messages"). Servem de
/// CONTRATO: o smoke test do build passa estes JSON pelo parser e pelo listener real. Quando
/// a Meta mudar o formato, grave aqui um payload novo (do painel "Test" do webhook) e o teste
/// mostra na hora o que quebrou. Também alimentam o simulador da tela.
/// </summary>
public static class WhatsAppPayloads
{
    public const string DirectorText = """
    {"object":"whatsapp_business_account","entry":[{"id":"102290129340398","changes":[{"field":"messages","value":{
      "messaging_product":"whatsapp",
      "metadata":{"display_phone_number":"5511900000000","phone_number_id":"106540352242922"},
      "contacts":[{"profile":{"name":"Diretor Industrial"},"wa_id":"5511988887777"}],
      "messages":[{"from":"5511988887777","id":"wamid.HBgNNTUxMTk4ODg4Nzc3NxUCABIYFDNBMDEA","timestamp":"1792300000","type":"text",
                   "text":{"body":"Preciso do status da bomba B-201 ainda hoje."}}]}}]}]}
    """;

    public const string TechnicianImage = """
    {"object":"whatsapp_business_account","entry":[{"id":"102290129340398","changes":[{"field":"messages","value":{
      "messaging_product":"whatsapp",
      "metadata":{"display_phone_number":"5511900000000","phone_number_id":"106540352242922"},
      "contacts":[{"profile":{"name":"Técnico de Campo"},"wa_id":"5531977776666"}],
      "messages":[{"from":"5531977776666","id":"wamid.HBgNNTUzMTk3Nzc3NjY2NhUCABIYFDNBMDIA","timestamp":"1792300060","type":"image",
                   "image":{"caption":"Vazamento no selo mecânico da B-201, falha recorrente","mime_type":"image/jpeg",
                            "sha256":"Q2hlY2tzdW0=","id":"1479537139650973"}}]}}]}]}
    """;

    public const string OffTopic = """
    {"object":"whatsapp_business_account","entry":[{"id":"102290129340398","changes":[{"field":"messages","value":{
      "messaging_product":"whatsapp",
      "metadata":{"display_phone_number":"5511900000000","phone_number_id":"106540352242922"},
      "contacts":[{"profile":{"name":"Colega"},"wa_id":"5511955554444"}],
      "messages":[{"from":"5511955554444","id":"wamid.HBgNNTUxMTk1NTU1NDQ0NBUCABIYFDNBMDMA","timestamp":"1792300120","type":"text",
                   "text":{"body":"Alguém vai no almoço hoje?"}}]}}]}]}
    """;

    /// <summary>Recibo de entrega de algo que NÓS enviamos: o parser deve ignorar (zero mensagens).</summary>
    public const string StatusOnly = """
    {"object":"whatsapp_business_account","entry":[{"id":"102290129340398","changes":[{"field":"messages","value":{
      "messaging_product":"whatsapp",
      "metadata":{"display_phone_number":"5511900000000","phone_number_id":"106540352242922"},
      "statuses":[{"id":"wamid.X","status":"delivered","timestamp":"1792300180","recipient_id":"5511988887777"}]}}]}]}
    """;

    public static readonly string[] All = { DirectorText, TechnicianImage, OffTopic };
}
