using System;
using System.Collections.Generic;
using System.Linq;

namespace dotNet_Project.Models
{
    public class PaginatedList<T> : List<T>
    {
        public int PageIndex { get; private set; }
        public int TotalPages { get; private set; }

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            // FIX: Dacă nu există rezultate (count=0), forțăm TotalPages să fie 1.
            // Astfel, în interfață va apărea "Page 1 of 1" în loc de "Page 1 of 0".
            if (TotalPages < 1)
            {
                TotalPages = 1;
            }

            this.AddRange(items);
        }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }
}