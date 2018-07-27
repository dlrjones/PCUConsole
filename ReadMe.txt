// Email Password
/*
The password for the SendMail portion of this app is stored in the file [backup_path]\PCUKey.txt (find backup_path in the config file).
The referenced library KeyMaster is used to decrypt the password at run time. There is another app called EncryptAndHash 
(found in \\Lapis\h_purchasing$\Purchasing\PMM IS data\HEMM Apps\Executables\ ) that you can use to change the password when that becomes necessary. The key to the encrypted file is: PCUpdate.
*/



// APPLICATION NOTES

NOTE: TO RUN FOR THE FIRST TIME, DO A PROJECT WIDE SEARCH FOR THE WORD "PRODUCTION". UpdateCost.UpdatePatientCharge() (~ LINE 160)
PatcrgChanges.UpdatePatientCharge() (~ LINE 72).  REMOVE COMMENTS FROM THE REFERENCES TO THE PRODUCTION DB AND COMMENT OUT THE TEST DB REFERENCES.
SET APP.CONFIG.<debug> TO FALSE. YOU CAN RUN THE APP WITHOUT UPDATING ANYTHING IN THE DB BY SETTING THE APP.CONFIG.<updateTables> VALUE TO FALSE. 
THIS WILL PRODUCE A LIST OF WHICH ITEMS WOULD BEEN UPDATED IN THE LOG FILE. THE LOG WILL ALSO SHOW YOU WHAT THE UPDATED PRICE WOULD BE 
(via CalculatePatientPrice(Hashtable) ~ Line 48)

This is used to recalculate the amount charged to patients, based on UWM's cost and a mark-up percentage.
It can be run from a scheculed task or from the UI applicaton PatientChargeUpdate. 

There is an Excel spreadsheet detailing the program flow through the relevent classes and their methods. This is shown for both the full and incremental modes. Find it here:
	\\Lapis\h_purchasing$\Purchasing\PMM IS data\HEMM Apps\SourceCode\PCUConsole\PCU Program Flow.xlsx

INITIAL CONDITIONS
	1)	PatientChargeUpdate has to have been run at least once in order to provide the dollar value limits and the associated mark-up (collectively known as the Tier Value) that this app uses. The output of PatientChargeUpdate is stored in the table dbo.uwm_PatientChargeTierLevels.

	2)	The table uwm_IVPItemCost needs to have been initialized with values drawn from the HEMM database. Do this by running a full update or use this script and Excel to manually insert the results:
									SELECT  distinct  SI.ITEM_ID, IVP.PRICE, ITEM_NO 
									FROM ITEM_VEND_PKG IVP 
								   JOIN ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID 
								   JOIN SLOC_ITEM SI ON IVP.ITEM_VEND_ID = SI.ITEM_VEND_ID 
								   JOIN ITEM ON ITEM.ITEM_ID = SI.ITEM_ID
								   WHERE IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) 
								   AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) 
								   AND LEN(SI.PAT_CHRG_NO) > 0 
								   AND SI.STAT = 1 
								   AND LEFT(SI.PAT_CHRG_NO,5) <> '40411' 
								   AND IVP.PRICE > 0 
								   ORDER BY SI.ITEM_ID 
		This has likely been done already.

		Alternatively, you can run this in the uwm_BIAdmin database using yesterday's backup. First truncate the uwm_IVPItemCost table
						TRUNCATE TABLE uwm_IVPItemCost
						INSERT INTO dbo.uwm_IVPItemCost
									SELECT  distinct  SI.ITEM_ID, IVP.PRICE, ITEM_NO 
									FROM [h-hemm].dbo.ITEM_VEND_PKG IVP 
								   JOIN [h-hemm].dbo.ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID 
								   JOIN [h-hemm].dbo.SLOC_ITEM SI ON IVP.ITEM_VEND_ID = SI.ITEM_VEND_ID 
								   JOIN [h-hemm].dbo.ITEM ON ITEM.ITEM_ID = SI.ITEM_ID
								   WHERE IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM [h-hemm].dbo.ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) 
								   AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM [h-hemm].dbo.ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) 
								   AND LEN(SI.PAT_CHRG_NO) > 0 
								   AND SI.STAT = 1 
								   AND LEFT(SI.PAT_CHRG_NO,5) <> '40411' 
								   AND IVP.PRICE > 0 
								   ORDER BY SI.ITEM_ID 
		

	3) The app takes three arguments. args[0] is the coded representation of which locations need to be updated, the second is the task to be performed and the third is the debug flag.
		The args[0] value 16 represents HMC only and 4 means MPOUS only. 20 refers to both HMC and MPOUS. A complete breakdown can be found in the file PCUpdate.cs ParseLocationCode()
		The value in args[1] will be either be the word "incremental" or the word "full". In day to day operation of this app, args[1] will be "incremental". Occassionally, when the dollar value limits and mark-ups change, the argument will be "full"
		The value for args[2] sets the connect str to BIAdmin db (see UpdatePatCharges.UpdatePatientCharge) to allow solely with the test tables.

OVERVIEW OF STEPS
	1)	Determine the locations that need to be checked and the nature of the task.		(typically HEMM + MPOUS and INCREMENTAL)

	2)	Capture the Price value from the HEMM ITEM_VEND_PKG table (see the script in Initial Conditions Step 2).
		
    3) For an incremental refresh, capture the entire contents of the uwm_BIAdmin.uwm_IVPItemCost table. Compare the cost values for each given item_ID. 
	    Then store the item_ID and the ITEM_VEND_PKG Cost in a seperate data structure. (UpdatePatientCharges:CompareCost())
		For a full refresh, use all of the items returned from the script (see Initial Conditions Step 2).

	4) Calculate the new PatientCharge value.
	
	5) Write the new PatientCharge value back to the H-HEMMDB.SLOC_ITEM table, PAT_CHRG_PRICE field.
		This also updates the REC_UPDATE_DATE and REC_UPDATE_USR_ID fields to trigger the HSM interface.
		(you check for a successful interface by finding the ITEM_NO in the most recent back up file \\h-hsmapp\ItemMaster_PROD\backup\ORSTORES001out.XXX 
		where XXX is an incrementing extension)
		For MPOUS, rebuild the LocationProcedureCode in D_INVENTORY_ITEMS

	6) Truncate uwm_BIAdmin uwm_IVPItemCost table and uwm_MPOUS_LocProcCode table
	
	7)  Refresh uwm_IVPItemCost using the script above (see Initial Conditions Step 2). 

	8)	  Refresh the uwm_MPOUS_LocProcCode table.    [This can be done manually with this: PointOfUse.RefreshPreviousValues()]
	
	When the program ends, uwm_BIAdmin.uwm_IVPItemCost holds the same values as the HEMM.ITEM_VEND_PKG table.

TABLES USED IN UWM_BIADMIN
         uwm_IVPItemCost -- HEMM.  This holds the last current item cost values (since the previous update to SLOC_ITEM PAT_CHRG_PRICE)
         uwm_PatientChargeTierLevels -- HEMM.  This hods the current Tier values which tells you which multiplier to use to get the pat_chrg values from a given item cost
		 uwm_MPOUS_LocProcCode -- MPOUS.  This holds the last current Location Procedure Code. Functionally, this table is similar to uwm_IVPItemCost.

         uwm_SLOC_ITEM -- HEMM.  Used to test this application; it's a stand-in for the real SLOC_ITEM table. It can be deleted when testing is done.               
		 [uwm_D_INVENTORY_ITEMS] -- MPOUS.  Used to test this application; it's a stand-in for the real D_INVENTORY_ITEMS table. It can be deleted when testing is done.


TABLE DEFINITIONS      
									CREATE TABLE [dbo].[uwm_IVPItemCost](
									[ITEM_ID] [int] NOT NULL,
									[COST] [money] NULL,
									[ITEM_NO] [varchar](20) NULL
										) ON [PRIMARY]


									CREATE TABLE [dbo].[uwm_uwm_PatientChargeTierLevels](
										[COSTLIMIT1] [float] NULL,
										[CL1_MULT] [float] NULL,
										[COSTLIMIT2] [float] NULL,
										[CL2_MULT] [float] NULL,
										[COSTLIMIT3] [float] NULL,
										[CL3_MULT] [float] NULL,
										[COSTLIMIT4] [float] NULL,
										[CL4_MULT] [float] NULL,
										[COSTLIMIT5] [float] NULL,
										[CL5_MULT] [float] NULL,
										[COSTLIMIT6] [float] NULL,
										[CL6_MULT] [float] NULL,
										[COSTLIMIT7] [float] NULL,
										[CL7_MULT] [float] NULL,
										[COSTLIMI8] [float] NULL,
										[CL8_MULT] [float] NULL,
										[COSTLIMIT9] [float] NULL,
										[CL9_MULT] [float] NULL,
										[COSTLIMIT10] [float] NULL,
										[CL10_MULT] [float] NULL,
										[CHANGE_DATE] [datetime] NULL
									) ON [PRIMARY]


									CREATE TABLE [dbo].[uwm_SLOC_ITEM](
										[ITEM_ID] [int] NOT NULL,
										[PAT_CHRG_PRICE] [money] NULL
									) ON [PRIMARY]


									CREATE TABLE [dbo].[uwm_MPOUS_LocProcCode](
										[MPOUS_ItemID] [float] NULL,
										[Alias_Id] [float] NULL,
										[Location_Procedure_Code] [nvarchar](255) NULL
									) ON [PRIMARY]

									
									CREATE TABLE [dbo].[uwm_D_INVENTORY_ITEMS](
										[ItemID] [float] NULL,
										[Alias_Id] [float] NULL,
										[Location_Procedure_Code] [nvarchar](255) NULL
									) ON [PRIMARY]

Run this before going full PRODUCTION to capture the current item price for each item (as a backup)
For HEMM:
							SELECT  distinct  SI.ITEM_ID, IVP.PRICE, ITEM_NO, 
							(SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) IVP_SEQ,
							(SELECT MIN(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) IV_SEQ
									FROM ITEM_VEND_PKG IVP 
								   JOIN ITEM_VEND IV ON IVP.ITEM_VEND_ID = IV.ITEM_VEND_ID 
								   JOIN SLOC_ITEM SI ON IVP.ITEM_VEND_ID = SI.ITEM_VEND_ID 
								   JOIN ITEM ON ITEM.ITEM_ID = SI.ITEM_ID
								   WHERE IVP.SEQ_NO = (SELECT MAX (SEQ_NO) FROM ITEM_VEND_PKG WHERE ITEM_VEND_ID = SI.ITEM_VEND_ID) 
								   AND IV.SEQ_NO = (SELECT MIN(SEQ_NO) FROM ITEM_VEND WHERE ITEM_VEND_ID = IVP.ITEM_VEND_ID) 
								   AND LEN(SI.PAT_CHRG_NO) > 0 
								   AND SI.STAT = 1 
								   AND LEFT(SI.PAT_CHRG_NO,5) <> '40411' 
								   AND IVP.PRICE > 0 
								   ORDER BY SI.ITEM_ID
								   
For MPOUS:							   
SELECT DISTINCT  DII.Item_Id, Alias_Id, [Location_Procedure_Code] 
                                            from [PointOfUseSupply].[dbo].[D_INVENTORY_ITEMS] DII  
                                            JOIN AHI_ITEM_ALIAS AIA ON AIA.ITEM_ID = DII.Item_Id 
                                            WHERE (LEN([Location_Procedure_Code]) > 0 AND  [Location_Procedure_Code] <> '0') 
                                            AND ACTIVE_FLAG = 1 
                                            AND BILLABLE_FLAG > 0								    