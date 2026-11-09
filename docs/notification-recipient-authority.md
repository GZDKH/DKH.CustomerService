# Notification recipient authority

[Русская версия](ru/notification-recipient-authority.md)

Customer#56 supports original unified-commerce V6.2/Commerce#88. The private
`NotificationRecipientService/GetBudgetChangeRecipient` RPC in
`proto.customer.api.notification_recipient.v2` returns current owner DATA for
the existing Notification service; it never acts as the recipient.

The request names the exact storefront/profile and original issuer/opaque
subject retained by Commerce. Existing `CustomerJournalAccounts` and
`CurrentActiveProfileBinding` resolve the current unique primary/verified-alias
account and active membership/profile. Known external/internal URLs of the
configured same Keycloak realm retain the existing canonical owner namespace.
Foreign/missing/revoked/ambiguous sources return NotFound without contact data.

Admission uses the existing Platform JWT signature/issuer/audience validator,
then request-local provenance captured only during successful validation.
The authenticated token must identify `service:v1`, azp
`dkh-notification-service`, its actual service-account username and unexpired
subject. Other services, humans and arbitrary injected claims cannot use the
method. Current caller/header/context state is rechecked around source waits.
Existing self/admin/DeliveryContact endpoints are unchanged; no new server
credential, configuration key, endpoint port or outbound client is added.

The model preserves canonical primary identity separately from profile ID,
current channel preferences, language, native verified-email observation and
a short read horizon. Never cast the profile/account ID to Keycloak UserId or
invent a Telegram recipient from legacy profile.UserId. Actual supported
identity-to-preference/device resolution belongs to Notification.

Email comes from the current canonical CustomerAccount verified contact and
its native verification time, governed by the exact profile's email preference.
A missing profile override uses that established primary contact. A different
explicit profile email returns UnverifiedProfileOverride with no email, even
if its legacy ContactVerification flag says verified; it does not silently
fall back. Disabled and unavailable verified-contact outcomes also contain no
destination. Customer#50's separate profile OTP concern is not declared fixed.
No preference is changed, customer provisioned/reconciled, verification carried
out or provider send performed by this read.

ObservedAt/ReadValidUntil (at most5seconds and original caller expiry) are DATA
freshness only. Binding and projected native source are rechecked before
return; changed source returns Aborted. Actual Notification dispatch must
resolve/revalidate current source and its own event/channel/device preferences,
deduplicate original event/business keys, enforce private routing and record
real provider outcomes. Queued stale contacts are not later-send permission.

Contracts change is additive1.12.0→1.13.0 through the existing version.json/CI
owner. Official SDK publication and actual Notification client/config/private
queue binding are separate integration gates. Deploy the actual admitted
consumer/private binding before activating Commerce recipient events. Native
fixtures use synthetic accounts and disposable PostgreSQL/HTTP2, never real
notification recipients or a production database. Source authoring is not
whole-service qualification, runtime delivery or original V6.2 acceptance.
