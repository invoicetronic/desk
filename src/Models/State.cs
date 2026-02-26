namespace Desk.Models;

public enum State
{
    Inviato = 2,
    Consegnato = 5,
    NonConsegnato = 6,
    Scartato = 7,
    AccettatoDalDestinatario = 8,
    RifiutatoDalDestinatario = 9,
    ImpossibilitaDiRecapito = 10,
    DecorrenzaTermini = 11,
    AttestazioneTrasmissioneFattura = 12
}
