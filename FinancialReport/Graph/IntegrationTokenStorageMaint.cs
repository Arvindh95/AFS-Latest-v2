using System;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport
{
  public class IntegrationTokenStorageMaint : PXGraph<IntegrationTokenStorageMaint>
  {
    // Basic View to select the token rows
    public SelectFrom<IntegrationTokenStorage>.View TokenRecords;

    // Optional: If you want standard "Save" and "Cancel" buttons
    public PXSave<IntegrationTokenStorage> Save;
    public PXCancel<IntegrationTokenStorage> Cancel;
    
    
    // Example: A method to get a token row by TokenType
    public IntegrationTokenStorage GetTokenRow(string tokenType)
    {
        return SelectFrom<IntegrationTokenStorage>
               .Where<IntegrationTokenStorage.tokenType.IsEqual<@P.AsString>>
               .View
               .Select(this, tokenType);
    }

    // Example: A method to upsert (insert or update) a token row
    public void UpsertTokenRow(
        string tokenType, 
        string accessToken, 
        string refreshToken, 
        DateTime? expiresAt)
    {
        var row = GetTokenRow(tokenType);
        if (row == null)
        {
            row = new IntegrationTokenStorage
            {
                TokenType = tokenType,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };
            TokenRecords.Insert(row);
        }
        else
        {
            row.AccessToken = accessToken;
            row.RefreshToken = refreshToken;
            row.ExpiresAt = expiresAt;
            TokenRecords.Update(row);
        }

        Actions.PressSave();
    }
  }
}