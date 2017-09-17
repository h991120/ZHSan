﻿using GameObjects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WorldOfTheThreeKingdomsEditor
{
    public abstract class BaseTab<T> where T : GameObject, new()
    {
        class ItemOrderComparer: IComparer<String>
        {
            private String[] rawItemOrder;
            public ItemOrderComparer(String[] rawItemOrder)
            {
                this.rawItemOrder = rawItemOrder;
            }

            Dictionary<String, int> order;
            public int Compare(string x, string y)
            {
                if (order == null)
                {
                    order = new Dictionary<string, int>();
                    int i = 0;
                    foreach (String s in rawItemOrder)
                    {
                        order.Add(s, i);
                        i++;
                    }
                }
                int xi, yi;
                if (!order.TryGetValue(x, out xi))
                {
                    xi = int.MaxValue;
                }
                if (!order.TryGetValue(y, out yi))
                {
                    yi = int.MaxValue;
                }
                return xi - yi;
            }
        }

        protected Type[] supportedTypes = new Type[]
        {
            typeof(bool),
            typeof(byte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(float),
            typeof(double),
            typeof(char),
            typeof(string)
        };

        private readonly T sampleInstance = Activator.CreateInstance<T>();

        protected FieldInfo[] getFieldInfos()
        {
            return sampleInstance.GetType().GetFields()
                .Where(x => Attribute.IsDefined(x, typeof(DataMemberAttribute)))
                .Where(x => supportedTypes.Contains(x.FieldType))
                .ToArray();
        }

        protected PropertyInfo[] getPropertyInfos()
        {
            return sampleInstance.GetType().GetProperties()
                .Where(x => Attribute.IsDefined(x, typeof(DataMemberAttribute)))
                .Where(x => supportedTypes.Contains(x.PropertyType))
                .ToArray();
        }

        private GameScenario scen;
        private DataGrid dg;
        
        protected void init(GameScenario scen, DataGrid dg)
        {
            this.scen = scen;
            this.dg = dg;
        }

        protected abstract String[] GetRawItemOrder();

        protected abstract Dictionary<String, String> GetDefaultValues();

        protected abstract GameObjectList GetDataList(GameScenario scen);

        public void setup()
        {
            DataTable dt = new DataTable(sampleInstance.GetType().Name);

            FieldInfo[] fields = getFieldInfos();
            PropertyInfo[] properties = getPropertyInfos();

            MemberInfo[] items = new MemberInfo[fields.Length + properties.Length];
            items = items.Union(fields).Union(properties).OrderBy(x => x == null ? "" : x.Name, new ItemOrderComparer(GetRawItemOrder())).ToArray();

            foreach (MemberInfo i in items)
            {
                if (i == null) continue;

                String name = i.Name;
                Type type;
                if (i is FieldInfo)
                {
                    type = ((FieldInfo)i).FieldType;
                }
                else
                {
                    type = ((PropertyInfo)i).PropertyType;
                }

                /*
                if (type.Name == "Nullable`1")
                {
                    type = type.GenericTypeArguments[0];
                }*/
                String defaultValue;
                if (!GetDefaultValues().TryGetValue(name, out defaultValue))
                {
                    if (type == typeof(string))
                    {
                        defaultValue = "";
                    }
                    else
                    {
                        defaultValue = "0";
                    }
                }

                DataColumn col = new DataColumn(name, type);
                col.DefaultValue = defaultValue;
                dt.Columns.Add(col);
            }

            foreach (T p in GetDataList(scen))
            {
                DataRow row = dt.NewRow();

                foreach (FieldInfo i in fields)
                {
                    row[i.Name] = i.GetValue(p);
                }
                foreach (PropertyInfo i in properties)
                {
                    row[i.Name] = i.GetValue(p) ?? DBNull.Value;
                }

                dt.Rows.Add(row);
            }

            dg.ItemsSource = dt.AsDataView();

            dt.TableNewRow += Dt_TableNewRow;
            dt.RowChanged += Dt_RowChanged;
            dt.RowDeleted += Dt_RowDeleted;
        }

        private void Dt_RowDeleted(object sender, DataRowChangeEventArgs e)
        {
            try
            {
                GameObjectList list = (GameObjectList)GetDataList(scen);
                T p = (T) list.GetGameObject((int)e.Row["id"]);
                list.Remove(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show("資料輸入錯誤。" + ex.Message);
            }
        }

        private void Dt_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            try
            {
                T p = (T)((GameObjectList)GetDataList(scen)).GetGameObject((int)e.Row["id"]);

                FieldInfo[] fields = getFieldInfos();
                PropertyInfo[] properties = getPropertyInfos();

                foreach (FieldInfo i in fields)
                {
                    i.SetValue(p, e.Row[i.Name]);
                }
                foreach (PropertyInfo i in properties)
                {
                    i.SetValue(p, e.Row[i.Name]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("資料輸入錯誤。" + ex.Message);
            }
        }

        private void Dt_TableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            T p = Activator.CreateInstance<T>();

            GameObjectList list = (GameObjectList)GetDataList(scen);
            int id = list.GetFreeGameObjectID();
            e.Row["id"] = id;
            p.ID = id;
            list.Add(p);
        }
    }
}
