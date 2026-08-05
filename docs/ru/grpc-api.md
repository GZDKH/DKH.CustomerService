# DKH.CustomerService -- gRPC API

> Translation Pending — см. [английскую версию](../grpc-api.md)

## Обзор

DKH.CustomerService предоставляет 10 gRPC-сервисов на порту **5010** (HTTP/2).

## CustomerAccountAdminService

**Пакет:** `proto.customer.api.customer_account_admin.v1`
**Версия Contracts:** `1.9.0`
**Авторизация:** только platform-роли `SuperAdmin`, realm `Admin` или `FullAccess`.

Сервис отделяет platform-global операции над аккаунтом от merchant-операций над
membership конкретной витрины. Он предоставляет поиск и пагинацию глобальных
аккаунтов, количество membership, безопасные provider badges, изменение
статусов, JSON-экспорт и анонимизирующее удаление. Чтения и разрушительные
операции записываются в структурированный аудит. Для изменений требуется
ограниченный machine-readable reason code; свободный текст с PII отклоняется и
не попадает в логи.

| Метод | Назначение |
|-------|------------|
| `ListCustomerAccounts` | Поиск и фильтрация глобальных аккаунтов |
| `GetCustomerAccount` | Детали аккаунта, число membership и безопасные provider metadata |
| `ListAccountStorefrontMemberships` | Membership аккаунта с фильтром по витрине и статусу |
| `SetCustomerAccountStatus` | Блокировка или активация глобального аккаунта |
| `SetStorefrontMembershipStatus` | Активация, блокировка или отзыв membership |
| `ExportCustomerAccount` | Безопасный JSON-экспорт |
| `DeleteCustomerAccount` | Анонимизация аккаунта и связанных данных |

Ответы не содержат provider authority, provider subject, provider user ID,
токены или другие необработанные идентификаторы провайдера.

## Privacy-safe метрики аккаунта клиента

Meter `DKH.CustomerService.CustomerAccount` экспортирует counters и latency
histograms для identity link/rejection/unlink/recovery и membership
first/returning/revoked touch. Labels ограничены определённым сервером
`storefront_id`, закрытым `outcome` и bounded `provider_kind` (`telegram`,
`email`, `wechat`, `google`, `apple` или `other`).

Email, телефон, имя, адрес, Keycloak/provider subject, customer/account/user id,
токены, секреты и произвольные ошибки никогда не становятся metric labels.
Неизвестный raw provider сворачивается в `other`; `CustomerAccountMetricsTests`
проверяет allowlist и отсутствие raw provider values в measurements. Это
агрегированные series без opaque attempt id, поэтому для них применяется общая
platform metrics retention policy.

*Последнее обновление: апрель 2026*
