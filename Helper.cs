public class Helper
{
    public static void ShareToUser(Guid callerUser, Guid caseId, IOrganizationService crmService)
    {
        GrantAccessRequest grantAccessRequest = new GrantAccessRequest
        {
            PrincipalAccess = new PrincipalAccess
            {
                Principal = new EntityReference("systemuser", callerUser),
                AccessMask = AccessRights.WriteAccess
            },
            Target = new EntityReference("incident", caseId)
        };
        crmService.Execute(grantAccessRequest);
    }

    public static void UnShareToUser(Guid callerUser, Guid caseId, IOrganizationService crmService)
    {

        RevokeAccessRequest revokeAccessRequest = new RevokeAccessRequest
        {
            Revokee = new EntityReference("systemuser", callerUser),
            Target = new EntityReference("incident", caseId)
        };
        crmService.Execute(revokeAccessRequest);
    }

    public static Guid? RetrieveTeamByQueueId(IOrganizationService crmService, Guid queueId, OptionSetValue caseType)
    {
        QueryExpression query = new QueryExpression("new_initializequeuesetting")
        {
            ColumnSet = new ColumnSet("new_targetowner"),
            Criteria =
            {
                Conditions = { 
                        new ConditionExpression("new_targetqueue",ConditionOperator.Equal,queueId)
                    }
            }
        };
        EntityCollection result = crmService.RetrieveMultiple(query);

        if (result.Entities != null && result.Entities.Count >= 1)
        {
            return (result[0]["new_targetowner"] as EntityReference).Id;
        }

        return null;
    }

    public static void CheckActionPermission(IOrganizationService crmService, Entity caseEntity, string errorMessage = "")
    {
        if (!caseEntity.Contains("new_casetype") || !caseEntity.Contains("new_casesubtype"))
        {
            return;
        }

        QueryExpression query = new QueryExpression("new_casetypesecuritysetting")
        {
            ColumnSet = new ColumnSet("new_casetypesecuritysettingid"),
            Criteria =
            {
                Conditions = { 
                        new ConditionExpression("new_casetype",ConditionOperator.Equal,(caseEntity["new_casetype"] as OptionSetValue).Value),
                        new ConditionExpression("new_casesubtype",ConditionOperator.Equal,(caseEntity["new_casesubtype"] as OptionSetValue).Value)
                    }
            }
        };

        EntityCollection result = crmService.RetrieveMultiple(query);

        if (result.Entities.Count == 0)
        {
            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = "You do not have enough privileges to update this case, contact your FieldOps Admin.";
            }
            throw new InvalidPluginExecutionException(errorMessage);
        }
    }



    public static string GetFetchXML()
    {
        return String.Format(@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
                                      <entity name='incident'>
                                        <attribute name='title' />
                                        <attribute name='ticketnumber' />
                                        <attribute name='mc_symptomtypeid' />
                                        <attribute name='mc_symptomsubtypeid' />
                                        <attribute name='statuscode' />
                                        <attribute name='modifiedon' />
                                        <attribute name='incidentid' />
                                        <order attribute='ticketnumber' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='statecode' operator='eq' value='0' />
                                        </filter>
                                      </entity>
                                    </fetch>");
    }

    public static List<Guid> ToGuidList(IEnumerable<Entity> entities)
    {
        var guids = new List<Guid>();

        foreach (Entity e in entities)
        {
            guids.Add(e.Id);
        }

        return guids;
    }

    public static EntityReferenceCollection CreateEntityReferenceCollection(List<Guid> guidList, string logicalName)
    {
        var entityRefCollection = new EntityReferenceCollection();

        foreach (Guid guid in guidList)
        {
            entityRefCollection.Add(new EntityReference(logicalName, guid));
        }

        return entityRefCollection;
    }

    public static void CopyRelatedEntities(IOrganizationService service, MappingProfile profile, Guid srcId, Guid dstId)
    {
        List<Guid> srcList = ToGuidList(RetrieveRelatedEntities(service, profile, srcId));
        List<Guid> destList = ToGuidList(RetrieveRelatedEntities(service, profile, dstId));
        srcList.RemoveAll(item => destList.Contains(item));

        EntityReferenceCollection erc = CreateEntityReferenceCollection(srcList, profile.RelationShip);

        AssociateRequest req = new AssociateRequest
        {
            Target = new EntityReference(profile.ToEntity, dstId),
            RelatedEntities = erc,
            Relationship = new Relationship(profile.RelationShip)
        };

        service.Execute(req);
    }

    public static DataCollection<Entity> RetrieveRelatedEntities(IOrganizationService service, MappingProfile profile, Guid entityId)
    {
        string fetchxml = @"
<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
  <entity name='" + profile.FromEntity + @"'>
    <attribute name='" + profile.FromEntity + @"id' />
    <link-entity name='" + profile.RelationShip + @"' from='" + profile.FromEntity + @"id' to='" + profile.FromEntity + @"id' visible='false' intersect='true'>
      <link-entity name='" + profile.ToEntity + @"' from='" + profile.ToEntity + @"id' to='" + profile.ToEntity + @"id' alias='ae'>
        <filter type='and'>
          <condition attribute='" + profile.ToEntity + @"id' operator='eq' value='" + entityId + @"' />
        </filter>
      </link-entity>
    </link-entity>
  </entity>
</fetch>"
;

        EntityCollection ec = service.RetrieveMultiple(new FetchExpression(fetchxml));
        //QueryExpression qry = ConvertFetchXmlToQuery(fetchxml, service);
        return ec.Entities;
    }


    public static DataCollection<Entity> RetrieveSharepointNotes(IOrganizationService service)
    {
        QueryExpression query = new QueryExpression("annotation")
        {
            //ColumnSet = new ColumnSet(true),
            Criteria =
            {
                Filters =
                    {
                        new FilterExpression(LogicalOperator.Or)
                        {
                            Conditions = 
                            { 
                                new ConditionExpression("subject",ConditionOperator.Equal, "Attachment Error"),
                            },

                            Filters =
                            {
                                new FilterExpression(LogicalOperator.And)
                                {
                                    Conditions =
                                    { 
                                       new ConditionExpression("subject", ConditionOperator.Equal, "File Attachment"),
                                        new ConditionExpression("notetext", ConditionOperator.Like, "http://spark.partners.extranet.microsoft.com%"),                        
                                    }
                                }
                            }
                        }
                    }
            }
        };

        EntityCollection ec = service.RetrieveMultiple(query);

        return ec.Entities;
    }

    public static string ConvertQueryToFetchXml(QueryExpression query, IOrganizationService crmService)
    {
        QueryExpressionToFetchXmlRequest request = new QueryExpressionToFetchXmlRequest();
        request.Query = query;
        QueryExpressionToFetchXmlResponse response
        = (QueryExpressionToFetchXmlResponse)crmService.Execute(request);
        return response.FetchXml;
    }

    public static QueryExpression ConvertFetchXmlToQuery(string fetchXml, IOrganizationService crmService)
    {
        FetchXmlToQueryExpressionRequest fetchXmlRequest = new FetchXmlToQueryExpressionRequest();
        fetchXmlRequest.FetchXml = fetchXml;
        FetchXmlToQueryExpressionResponse fetchXmlResponse
        = (FetchXmlToQueryExpressionResponse)crmService.Execute(fetchXmlRequest);
        return fetchXmlResponse.Query;
    }

    public static IOrganizationService CreateService(string url, string user, string password, string domain)
    {
        Uri organizationUri = new Uri(url);
        Uri homeRealmUri = null;
        ClientCredentials credentials = new ClientCredentials();
        NetworkCredential networkCredential = new NetworkCredential(user, password);
        credentials.Windows.ClientCredential = networkCredential;

        OrganizationServiceProxy orgProxy = new OrganizationServiceProxy(organizationUri, homeRealmUri, credentials, null);

        return (IOrganizationService)orgProxy;

    }

    //string name  = GetPickListText("opportunity", "new_opportunitystate", 0, service);
    public static string GetPickListText(string entityName, string attributeName, int optionSetValue, IOrganizationService service)
    {
        string AttributeName = attributeName;
        string EntityLogicalName = entityName;

        RetrieveEntityRequest retrieveDetails = new RetrieveEntityRequest();
        retrieveDetails.EntityFilters = EntityFilters.All;
        retrieveDetails.LogicalName = EntityLogicalName;


        RetrieveEntityResponse retrieveEntityResponseObj = (RetrieveEntityResponse)service.Execute(retrieveDetails);
        EntityMetadata metadata = retrieveEntityResponseObj.EntityMetadata;

        PicklistAttributeMetadata picklistMetadata = metadata.Attributes.FirstOrDefault(attribute => String.Equals(attribute.LogicalName, attributeName, StringComparison.OrdinalIgnoreCase)) as PicklistAttributeMetadata;

        OptionSetMetadata options = picklistMetadata.OptionSet;
        IList<OptionMetadata> picklistOption = (from o in options.Options where o.Value.Value == optionSetValue select o).ToList();
        string picklistLabel = (picklistOption.First()).Label.UserLocalizedLabel.Label;

        return picklistLabel;

    }

    public static void ChangeRecordState(IOrganizationService service, EntityReference entityMoniker, OptionSetValue state, OptionSetValue status)
    {
        SetStateRequest req = new SetStateRequest
        {
            EntityMoniker = entityMoniker,
            State = state,
            Status = status
        };
        service.Execute(req);
    }

    public static void CreateEmailFromTemplate(IOrganizationService service, EntityCollection sendFromCol, EntityCollection sendToCol, string emailSubject, string previewTriggerVaule, EntityReference regard, Guid contactId, Entity template, string btnOrder)
    {
        if (template != null)
        {
            // Use the InstantiateTemplate message to create an e-mail message using a template.
            InstantiateTemplateRequest instTemplateReq = new InstantiateTemplateRequest
            {
                TemplateId = template.Id,
                ObjectId = contactId,
                ObjectType = "contact"
            };
            InstantiateTemplateResponse instTemplateResp = (InstantiateTemplateResponse)service.Execute(instTemplateReq);
            Entity email = instTemplateResp.EntityCollection.Entities[0];
            email.Attributes["new_campaigninvatationid"] = previewTriggerVaule;
            email.Attributes["subject"] = emailSubject;
            email.Attributes.Add("from", sendFromCol);
            service.Create(email);
        }
    }

    public static EntityCollection RetrieveNNRecords(IOrganizationService service, Guid ToEntityId, string FromEntity, string ToEntity, string RelationShip)
    {
        QueryExpression qe = new QueryExpression()
        {
            EntityName = FromEntity,
            ColumnSet = new ColumnSet(true),
            Criteria =
            {
                FilterOperator = LogicalOperator.And,
                Conditions = 
                        {
                            new ConditionExpression("statecode",ConditionOperator.Equal,0)
                        }
            },
            LinkEntities = 
                    {
                       new LinkEntity()
                       {
                           LinkFromEntityName = FromEntity,
                           LinkToEntityName = RelationShip,
                           LinkFromAttributeName = FromEntity+"id",
                           LinkToAttributeName = FromEntity+"id",
                           EntityAlias = RelationShip,
                           Columns = new ColumnSet(),
                           JoinOperator = JoinOperator.Inner,
                           LinkEntities =
                           {
                               new LinkEntity()
                               {
                                   LinkFromEntityName = FromEntity,
                                   LinkToEntityName = ToEntity,
                                   LinkFromAttributeName = ToEntity+"id",
                                   LinkToAttributeName = ToEntity+"id",
                                   EntityAlias =ToEntity,
                                   Columns = new ColumnSet(),
                                   JoinOperator = JoinOperator.Inner,
                                   LinkCriteria = new FilterExpression
                                   {
                                       Conditions = 
                                       {
                                           new ConditionExpression
                                           {
                                               AttributeName = ToEntity+"id",
                                               Operator = ConditionOperator.Equal,
                                               Values = {
                                                   ToEntityId
                                               }
                                           }
                                       }
                                   }
                               }
                           }
                       }
                    }
        };

        return service.RetrieveMultiple(qe);
    }
}