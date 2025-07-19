using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NexusAIO.Features.DatabaseGroup.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shell;

namespace NexusAIO.Core.Database.Tools
{
    public class DatabaseCoordinator
    {
        private readonly DbContext _context;

        #region Dictionnaries
        /// <summary>
        /// Dictionnary regrouping all entities with there dependancies
        /// </summary>
        public Dictionary<IEntityType, List<IEntityType>> EntityTypesAndForeignKey { get; set; }
        public Dictionary<IEntityType, List<IEntityType>> EntityTypesAndMandatoryForeignKey { get; set; }

        /// <summary>
        /// the dictionnary to use when looping on an entity properties to feed 
        /// </summary>
        public Dictionary<Type, List<Type>> EntitiesAndSelectedProperties { get; set; }

        /// <summary>
        /// Dictionnary regrouping all entities with there dependancies but reduced to ensure maximal filling without conflict
        /// </summary>
        public Dictionary<IEntityType, List<IEntityType>> EntityTypesAndSelectedForeignKey { get; set; }

        public Dictionary<IEntityType, List<IEntityType>> DependancyForgotten { get; set; }

        #endregion

        #region Lists
        public List<IEntityType> EntityTypes { get; set; }

        public List<IEntityType> SeedingOrderEntities { get; set; }
        #endregion

        #region Schematics
        public List<List<IEntityType>> GroupedAndOrderedWithoutLost { get; set; }
        public List<List<IEntityType>> GroupedAndOrderedWithReducedLost { get; set; }


        #endregion

        public DatabaseCoordinator(DbContext context)
        {
            _context = context;
            GroupedAndOrderedWithoutLost = new List<List<IEntityType>>();
            GroupedAndOrderedWithReducedLost = new List<List<IEntityType>>();
            EntityTypesAndForeignKey = new Dictionary<IEntityType, List<IEntityType>>();
            EntityTypesAndMandatoryForeignKey = new Dictionary<IEntityType, List<IEntityType>>();
            EntityTypesAndSelectedForeignKey = new Dictionary<IEntityType, List<IEntityType>>();
            DependancyForgotten = new Dictionary<IEntityType, List<IEntityType>>();
            SeedingOrderEntities = new List<IEntityType>();

            RecoverAllEntityTypesAndForeignKey();
            BuildLists();
        }
        public void GetEntitiesTypesInSeedingOrder()
        {
            RecoverAllEntityTypesAndForeignKey();
            BuildLists();

        }
        private void RecoverAllEntityTypesAndForeignKey()
        {
            EntityTypes = _context.Model.GetEntityTypes().ToList();
            foreach (var entityType in EntityTypes)
            {
                List<IEntityType> allDependencies = entityType.GetForeignKeys()
            .Select(fk => fk.PrincipalEntityType)
            .ToList();


                EntityTypesAndForeignKey.Add(entityType, allDependencies);

                List<IEntityType> MandatoryOnly = entityType.GetForeignKeys()
                    .Where(fk => fk.IsRequired)
                    .Select(fk => fk.PrincipalEntityType)
                    .ToList();


                if(MandatoryOnly.Count > 0)
                {
                    EntityTypesAndMandatoryForeignKey.Add(entityType, MandatoryOnly);
                }
            }
        }

        private void BuildLists()
        {
            List<IEntityType> ConsideredTypes = new List<IEntityType>();
            List<IEntityType> RemainingTypes = new List<IEntityType>(EntityTypes);
            List<IEntityType> TempList = new List<IEntityType>(); //List to extract from dictionnary
            List<IEntityType> TurnList; //List accepted this turn


            bool IsAcceptable = true;

            while (RemainingTypes.Count > 0)
            {
                TurnList = new List<IEntityType>();

                foreach (var entityType in RemainingTypes)
                {

                    //on vérifie si il existe une foreign key qui n'a pas encore été considére
                    IsAcceptable = false;
                    TempList = new List<IEntityType>();

                    if (EntityTypesAndForeignKey.TryGetValue(entityType, out TempList))
                    {
                        IsAcceptable = true;
                        foreach (var value in TempList)
                        {
                            if (RemainingTypes.Contains(value))
                            {
                                IsAcceptable = false;
                            }
                        }

                        if (IsAcceptable)
                        {
                            TurnList.Add(entityType);

                        }
                    }
                    else
                    {
                        throw new Exception("truc bizare");
                    }


                    //on va vérifier que toute ses keys ne sont pas dans les entités 

                }
                if (TurnList.Count == 0)
                {
                    //VerifyPerfectStatus(ConsideredTypes,RemainingTypes);
                    ContinueWithWasteReduction(ConsideredTypes, RemainingTypes);

                    break;

                }
                ConsideredTypes.AddRange(TurnList);
                GroupedAndOrderedWithoutLost.Add(TurnList);
                foreach (IEntityType entityType in TurnList)
                {
                    RemainingTypes.Remove(entityType);
                }
            }

        }
        private ModifyingDictionnaries CreateMatrix(List<IEntityType> ConsideredTypes, List<IEntityType> RemainingTypes)
        {
            ModifyingDictionnaries modifyingDictionnaries = new ModifyingDictionnaries();

            //On fabrique la matrice des restants
            foreach (IEntityType type in RemainingTypes)
            {
                List<IEntityType> ReworkedList = EntityTypesAndForeignKey.GetValueOrDefault(type);
                List<IEntityType> TempReworkedList = new List<IEntityType>(ReworkedList);
                //On enlève les entités qui sont déja considérés des dépendances
                foreach (IEntityType itemtocheck in ReworkedList)
                {
                    if (ConsideredTypes.Contains(itemtocheck))
                    {
                        TempReworkedList.Remove(itemtocheck);
                    }
                }
                modifyingDictionnaries.Remainings.Add(type, TempReworkedList);

            }

            //On fabrique la matrice inversé
            foreach (var type in modifyingDictionnaries.Remainings)
            {
                foreach (var value in type.Value)
                {
                    if (modifyingDictionnaries.ReverseDictionary.ContainsKey(value))
                    {
                        modifyingDictionnaries.ReverseDictionary[value].Add(type.Key);
                    }
                    else
                    {
                        modifyingDictionnaries.ReverseDictionary.Add(value, new List<IEntityType>());
                        modifyingDictionnaries.ReverseDictionary[value].Add(type.Key);
                    }
                }
            }

            //o fabrique la liste qui ont des required encore non répondu
            foreach(var type in RemainingTypes)
            {
                List<IEntityType> ListMandories = new List<IEntityType>();
                List<IEntityType> TempListMandories = new List<IEntityType>();
                if(EntityTypesAndMandatoryForeignKey.TryGetValue(type, out ListMandories))
                {
                    TempListMandories = new List<IEntityType>(ListMandories);
                    foreach (var mandory in ListMandories)
                    {
                        if (ConsideredTypes.Contains(mandory))
                        {
                            TempListMandories.Remove(mandory);
                        }
                    }

                    if(TempListMandories.Count > 0)
                    {
                        modifyingDictionnaries.EntitiesWithRequiredDependancy.Add(type);
                    }
                }
            }

            return modifyingDictionnaries;
        }

        private void ContinueWithWasteReduction(List<IEntityType> ConsideredTypes, List<IEntityType> RemainingTypes)
        {
            ModifyingDictionnaries mD = new ModifyingDictionnaries();
            //Maintenant on fait dans l'ordre du moins pire en commencant par le plus de résultat sur la dépendance inversée sur une boucle

            List<TypeWithNbDependancies> MyList = new List<TypeWithNbDependancies>();
            List<IEntityType> SelectedList = new List<IEntityType>();
            bool found;

            while (RemainingTypes.Count > 0)
            {
                mD = CreateMatrix(ConsideredTypes, RemainingTypes);
                found = false;
                //D'abord on vérifie si il y en a sans dépendances
                foreach (var value in mD.Remainings)
                {
                    if (!value.Value.Any())
                    {
                        SelectedList.Add(value.Key);
                        ConsideredTypes.Add(value.Key);
                        RemainingTypes.Remove(value.Key);
                        found = true;
                    }
                }

                if (!found)
                {
                    //on choisi le plus bas et on extrait les possibles liens des autres entités
                    MyList = new List<TypeWithNbDependancies>();

                    //on rempli la list, on ordo et on prend le premier
                    foreach (var type in mD.ReverseDictionary)
                    {
                        MyList.Add(new TypeWithNbDependancies()
                        {
                            Type = type.Key,
                            NBForeignKey = type.Value.Count,
                        });
                    }

                    //On enleve ceux qui ont des propriétés Obligatoires
                    List<TypeWithNbDependancies> Templist = new List<TypeWithNbDependancies>(MyList);
                    foreach(var type in MyList)
                    {
                        if (mD.EntitiesWithRequiredDependancy.Contains(type.Type))
                        {
                            Templist.Remove(type);
                        }
                    }

                    //au cas où il n'y as que des entités dépendantes 
                    if(Templist.Count == 0)
                    {
                        MessageBox.Show("Il y a un probleme de circle dans els dépendances");
                    }


                    var Selected = MyList
                        .OrderBy(x => x.NBForeignKey)
                        .LastOrDefault();


                    DependancyForgotten.Add(Selected.Type, mD.Remainings.GetValueOrDefault(Selected.Type));

                    SelectedList.Add(Selected.Type);
                    ConsideredTypes.Add(Selected.Type);
                    RemainingTypes.Remove(Selected.Type);
                }
            }


            SeedingOrderEntities.AddRange(ConsideredTypes);

        }

    }

    public class TypeWithNbDependancies()
    {
        public IEntityType Type { get; set; }
        public int NBForeignKey { get; set; }
    } 

    public class ModifyingDictionnaries
    {
        public Dictionary<IEntityType, List<IEntityType>> Remainings { get; set; }
        public Dictionary<IEntityType, List<IEntityType>> ReverseDictionary { get; set; }
        public List<IEntityType> EntitiesWithRequiredDependancy { get; set; }
        public ModifyingDictionnaries()
        {
            Remainings = new Dictionary<IEntityType, List<IEntityType>>();
            ReverseDictionary = new Dictionary<IEntityType, List<IEntityType>>();
            EntitiesWithRequiredDependancy = new List<IEntityType>();
        }
    }
}
