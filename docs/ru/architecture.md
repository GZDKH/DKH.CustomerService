# DKH.CustomerService -- Архитектура

> Translation Pending — см. [английскую версию](../architecture.md)

## Обзор

DKH.CustomerService — микросервис на .NET 10, построенный по принципам Clean Architecture с CQRS (MediatR). Управляет профилями клиентов, адресами доставки, списками желаний, предпочтениями, верификацией контактов и привязкой внешних идентификаторов для витрин Telegram Mini App.

AdminGateway использует отдельный platform-global API для аккаунтов и сохраняет
явное storefront-scoped управление membership клиентов для merchant-ролей.

*Последнее обновление: март 2026*
