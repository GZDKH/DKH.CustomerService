namespace DKH.CustomerService.Infrastructure.DeliveryContacts;

internal static class DeliveryContactAuthoritySql
{
    public const string Install = """
        CREATE FUNCTION public.customer_delivery_contact_immutable() RETURNS trigger
        LANGUAGE plpgsql AS $body$
        BEGIN
          IF TG_OP = 'UPDATE' AND TG_TABLE_NAME = 'delivery_contact_consents' THEN
            IF OLD."RevokedAtUtc" IS NULL AND OLD."Revision" = 1
               AND NEW."RevokedAtUtc" IS NOT NULL AND NEW."RevokedAtUtc" >= OLD."CreatedAtUtc"
               AND NEW."RevokeOperationId" IS NOT NULL AND NEW."RevokeRequestDigest" ~ '^[0-9a-f]{64}$'
               AND NEW."Revision" = 2
               AND (to_jsonb(NEW) - ARRAY['Revision','RevokedAtUtc','RevokeOperationId','RevokeRequestDigest','LastModificationTime','LastModifierId'])
                 = (to_jsonb(OLD) - ARRAY['Revision','RevokedAtUtc','RevokeOperationId','RevokeRequestDigest','LastModificationTime','LastModifierId']) THEN
              RETURN NEW;
            END IF;
          END IF;
          RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'Delivery contact authority metadata is immutable except explicit consent revocation.';
        END;
        $body$;
        CREATE TRIGGER delivery_contact_revision_immutable BEFORE UPDATE OR DELETE ON public.delivery_contact_revisions
          FOR EACH ROW EXECUTE FUNCTION public.customer_delivery_contact_immutable();
        CREATE TRIGGER delivery_contact_consent_immutable BEFORE UPDATE OR DELETE ON public.delivery_contact_consents
          FOR EACH ROW EXECUTE FUNCTION public.customer_delivery_contact_immutable();
        CREATE TRIGGER delivery_contact_lease_immutable BEFORE UPDATE OR DELETE ON public.delivery_contact_leases
          FOR EACH ROW EXECUTE FUNCTION public.customer_delivery_contact_immutable();
        CREATE TRIGGER delivery_contact_revision_truncate_immutable BEFORE TRUNCATE ON public.delivery_contact_revisions
          FOR EACH STATEMENT EXECUTE FUNCTION public.customer_delivery_contact_immutable();
        CREATE TRIGGER delivery_contact_consent_truncate_immutable BEFORE TRUNCATE ON public.delivery_contact_consents
          FOR EACH STATEMENT EXECUTE FUNCTION public.customer_delivery_contact_immutable();
        CREATE TRIGGER delivery_contact_lease_truncate_immutable BEFORE TRUNCATE ON public.delivery_contact_leases
          FOR EACH STATEMENT EXECUTE FUNCTION public.customer_delivery_contact_immutable();
        """;

    public const string RefusePopulatedDown = """
        DO $body$
        BEGIN
          LOCK TABLE public.delivery_contact_revisions, public.delivery_contact_consents, public.delivery_contact_leases IN ACCESS EXCLUSIVE MODE;
          IF EXISTS (SELECT 1 FROM public.delivery_contact_revisions)
             OR EXISTS (SELECT 1 FROM public.delivery_contact_consents)
             OR EXISTS (SELECT 1 FROM public.delivery_contact_leases) THEN
            RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'Delivery contact authority history must be retained before downgrade.';
          END IF;
        END;
        $body$;
        """;

    public const string Remove = """
        DROP TRIGGER delivery_contact_revision_immutable ON public.delivery_contact_revisions;
        DROP TRIGGER delivery_contact_consent_immutable ON public.delivery_contact_consents;
        DROP TRIGGER delivery_contact_lease_immutable ON public.delivery_contact_leases;
        DROP TRIGGER delivery_contact_revision_truncate_immutable ON public.delivery_contact_revisions;
        DROP TRIGGER delivery_contact_consent_truncate_immutable ON public.delivery_contact_consents;
        DROP TRIGGER delivery_contact_lease_truncate_immutable ON public.delivery_contact_leases;
        DROP FUNCTION public.customer_delivery_contact_immutable();
        """;
}
